using System.Data.Common;
using Tradebook.Core.Messaging;
using Wolverine;
using Wolverine.RDBMS;
using Wolverine.Runtime;

namespace Tradebook.Api.Messaging;

public sealed class WolverineTransactionalEventPublisher(IWolverineRuntime runtime)
    : ITransactionalEventPublisher
{
    private static readonly TimeSpan StartupWaitStep = TimeSpan.FromMilliseconds(100);
    private const int StartupWaitAttempts = 100;

    private readonly MessageContext _context = new(runtime);

    public Task EnlistAsync(DbTransaction transaction, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = (IMessageDatabase)_context.Storage;
        return _context.EnlistInOutboxAsync(new DatabaseEnvelopeTransaction(database, transaction));
    }

    public async ValueTask PublishAsync(EntityChangedDomainEvent domainEvent)
    {
        // Wolverine boots on a background retry loop via ResilientStartupHostedService,
        // so a request racing the deferred start briefly waits instead of failing. The
        // database is necessarily reachable here — the caller is inside an open
        // transaction — which means the runtime start completes within the bounded wait.
        var attempt = 0;
        while (true)
        {
            try
            {
                await _context.PublishAsync(domainEvent).ConfigureAwait(false);
                return;
            }
            catch (WolverineHasNotStartedException) when (attempt < StartupWaitAttempts)
            {
                attempt++;
                await Task.Delay(StartupWaitStep).ConfigureAwait(false);
            }
        }
    }

    public Task FlushAsync() => _context.FlushOutgoingMessagesAsync();
}
