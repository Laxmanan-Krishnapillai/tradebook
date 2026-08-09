using System.Data.Common;

namespace Tradebook.Core.Messaging;

public interface ITransactionalEventPublisher
{
    Task EnlistAsync(DbTransaction transaction, CancellationToken cancellationToken);

    ValueTask PublishAsync(EntityChangedDomainEvent domainEvent);

    Task FlushAsync();
}
