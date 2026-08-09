namespace Tradebook.Infrastructure.Outbox;

public sealed record OutboxEventRecord(
    Guid EventId,
    long SequenceId,
    string AggregateType,
    string AggregateId,
    string EventType,
    string Payload
);
