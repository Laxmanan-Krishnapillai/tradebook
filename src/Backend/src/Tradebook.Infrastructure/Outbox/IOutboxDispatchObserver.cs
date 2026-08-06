namespace Tradebook.Infrastructure.Outbox;

/// <summary>
/// Test seam required by Task 03 §6 T4: lets integration tests inject a failure between
/// fan-out and the mark-processed statement to prove at-least-once redelivery. Not
/// registered in production; the dispatcher treats a missing registration as a no-op.
/// </summary>
public interface IOutboxDispatchObserver
{
    Task BeforeMarkProcessedAsync(IReadOnlyList<OutboxEventRecord> batch, CancellationToken cancellationToken);
}
