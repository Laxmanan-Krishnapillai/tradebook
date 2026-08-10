using System.Data.Common;
using Tradebook.Core.Messaging;
using Wolverine.RDBMS;
using Wolverine.Runtime;

namespace Tradebook.Api.Messaging;

public sealed class WolverineTransactionalEventPublisher(IWolverineRuntime runtime)
    : ITransactionalEventPublisher
{
    private readonly MessageContext _context = new(runtime);

    public Task EnlistAsync(DbTransaction transaction, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = (IMessageDatabase)_context.Storage;
        return _context.EnlistInOutboxAsync(new DatabaseEnvelopeTransaction(database, transaction));
    }

    public ValueTask PublishAsync(EntityChangedDomainEvent domainEvent) =>
        _context.PublishAsync(domainEvent);

    public Task FlushAsync() => _context.FlushOutgoingMessagesAsync();
}
