using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;

namespace Tradebook.Core.DTOs;

public sealed record EntityChangedEventDto
{
    public EntityChangedEventDto() { }

    [SetsRequiredMembers]
    public EntityChangedEventDto(
        EventId EventId,
        long SequenceId,
        string AggregateType,
        string AggregateId,
        string EventType,
        string PayloadJson
    )
    {
        this.EventId = EventId;
        this.SequenceId = SequenceId;
        this.AggregateType = AggregateType;
        this.AggregateId = AggregateId;
        this.EventType = EventType;
        this.PayloadJson = PayloadJson;
    }

    public required EventId EventId { get; init; }

    public required long SequenceId { get; init; }

    public required string AggregateType { get; init; }

    public required string AggregateId { get; init; }

    public required string EventType { get; init; }

    public required string PayloadJson { get; init; }
}
