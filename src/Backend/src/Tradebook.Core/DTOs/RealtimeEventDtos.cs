using Tradebook.Core.Domain.ValueObjects.Ids;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record EntityChangedEventDto(
    EventId EventId,
    long SequenceId,
    string AggregateType,
    string AggregateId,
    string EventType,
    string PayloadJson
);

[ExportTsInterface]
public sealed record GetEventsSinceResponse(
    IReadOnlyList<EntityChangedEventDto> Events,
    long LatestSequence
);
