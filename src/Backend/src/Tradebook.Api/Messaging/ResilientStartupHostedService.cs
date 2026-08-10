using Microsoft.Extensions.Hosting;

namespace Tradebook.Api.Messaging;

/// <summary>
/// Starts an inner hosted service on a background retry loop so a dependency that is
/// down at boot (PostgreSQL for Wolverine's envelope storage) delays that service
/// instead of aborting the whole host. Liveness stays healthy while readiness reports
/// the unavailable database, preserving the task-02 boot contract.
/// </summary>
public sealed class ResilientStartupHostedService(
    Func<IServiceProvider, IHostedService> innerFactory,
    IServiceProvider services,
    ILogger<ResilientStartupHostedService> logger
) : IHostedService, IAsyncDisposable
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly CancellationTokenSource _stopping = new();
    private IHostedService? _inner;
    private bool _started;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var inner = innerFactory(services);
        _inner = inner;
        _ = Task.Run(() => StartWithRetryAsync(inner), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await CancelStoppingAsync().ConfigureAwait(false);
        if (!_started || _inner is null)
        {
            return;
        }

        try
        {
            await _inner.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Shutdown of a service whose dependency is unreachable must not turn a
            // graceful stop into a crash; the envelope store is durable regardless.
            ResilientStartupLog.StopFaulted(logger, _inner.GetType().Name, exception);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CancelStoppingAsync().ConfigureAwait(false);
        _stopping.Dispose();
        try
        {
            if (_inner is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else if (_inner is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch (Exception exception)
        {
            ResilientStartupLog.StopFaulted(logger, _inner?.GetType().Name ?? "inner", exception);
        }
    }

    private async Task CancelStoppingAsync()
    {
        try
        {
            await _stopping.CancelAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // Host dispose can precede or repeat stop; a disposed source already
            // means "stop everything", which is the state we wanted.
        }
    }

    private async Task StartWithRetryAsync(IHostedService inner)
    {
        while (!_stopping.IsCancellationRequested)
        {
            try
            {
                await inner.StartAsync(_stopping.Token).ConfigureAwait(false);
                _started = true;
                ResilientStartupLog.ServiceStarted(logger, inner.GetType().Name);
                return;
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                ResilientStartupLog.StartDeferred(logger, inner.GetType().Name, exception);
            }

            try
            {
                await Task.Delay(RetryDelay, _stopping.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
