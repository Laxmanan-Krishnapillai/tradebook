namespace Tradebook.Core.Domain.Entities;

public sealed class OutboxEvent
{
    public Guid EventId { get; init; }
    public long SequenceId { get; init; }
    public required string AggregateType { get; init; }
    public required string AggregateId { get; init; }
    public required string EventType { get; init; }
}
