using System.Collections.Concurrent;
using System.Data.Common;
using Tradebook.Core.Messaging;

namespace Tradebook.IntegrationTests;

internal sealed class RecordingTransactionalEventPublisher : ITransactionalEventPublisher
{
    public ConcurrentQueue<DbTransaction> Transactions { get; } = new();

    public ConcurrentQueue<EntityChangedDomainEvent> Events { get; } = new();

    public int FlushCount => Volatile.Read(ref _flushCount);

    private int _flushCount;

    public Task EnlistAsync(DbTransaction transaction, CancellationToken cancellationToken)
    {
        Transactions.Enqueue(transaction);
        return Task.CompletedTask;
    }

    public ValueTask PublishAsync(EntityChangedDomainEvent domainEvent)
    {
        Events.Enqueue(domainEvent);
        return ValueTask.CompletedTask;
    }

    public Task FlushAsync()
    {
        Interlocked.Increment(ref _flushCount);
        return Task.CompletedTask;
    }
}
