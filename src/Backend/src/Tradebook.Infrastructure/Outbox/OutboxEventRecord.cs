namespace Tradebook.Infrastructure.Outbox;

public sealed record OutboxEventRecord(Guid EventId, long SequenceId, string AggregateType,
    Guid AggregateId, string EventType, string Payload);
