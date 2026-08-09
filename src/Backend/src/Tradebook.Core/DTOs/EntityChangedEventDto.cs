using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record EntityChangedEventDto
{
    public EntityChangedEventDto() { }

    [SetsRequiredMembers]
    public EntityChangedEventDto(
        Guid EventId,
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

    public required Guid EventId { get; init; }

    public required long SequenceId { get; init; }

    public required string AggregateType { get; init; }

    public required string AggregateId { get; init; }

    public required string EventType { get; init; }

    public required string PayloadJson { get; init; }
}
