namespace Tradebook.UnitTests;

internal sealed class UnitTestNoopPublisher : Tradebook.Core.Messaging.ITransactionalEventPublisher
{
    public Task EnlistAsync(
        System.Data.Common.DbTransaction transaction,
        CancellationToken cancellationToken
    ) => Task.CompletedTask;

    public ValueTask PublishAsync(Tradebook.Core.Messaging.EntityChangedDomainEvent domainEvent) =>
        ValueTask.CompletedTask;

    public Task FlushAsync() => Task.CompletedTask;
}
