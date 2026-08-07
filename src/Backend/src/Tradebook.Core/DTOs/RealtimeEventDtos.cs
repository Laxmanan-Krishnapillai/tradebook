using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record EntityChangedEventDto(
    Guid EventId,
    long SequenceId,
    string AggregateType,
    string AggregateId,
    string EventType,
    string PayloadJson);

[ExportTsInterface]
public sealed record GetEventsSinceResponse(
    IReadOnlyList<EntityChangedEventDto> Events,
    long LatestSequence);
