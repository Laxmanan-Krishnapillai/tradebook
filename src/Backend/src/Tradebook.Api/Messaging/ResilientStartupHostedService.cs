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
            LogStopFaultedSafely(_inner.GetType().Name, exception);
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
            LogStopFaultedSafely(_inner?.GetType().Name ?? "inner", exception);
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

    private void LogStopFaultedSafely(string serviceName, Exception exception)
    {
        try
        {
            ResilientStartupLog.StopFaulted(logger, serviceName, exception);
        }
        catch (ObjectDisposedException)
        {
            // The host can dispose test or Windows Event Log providers before a
            // late inner-service shutdown fault is reported. Logging must not
            // turn an otherwise graceful shutdown into a second failure.
        }
        catch (AggregateException aggregate)
            when (aggregate
                    .Flatten()
                    .InnerExceptions.All(static inner => inner is ObjectDisposedException)
            )
        {
            // Microsoft.Extensions.Logging aggregates provider failures.
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
