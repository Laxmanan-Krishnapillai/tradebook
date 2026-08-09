using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record GetEventsSinceResponse
{
    public GetEventsSinceResponse() { }

    [SetsRequiredMembers]
    public GetEventsSinceResponse(IReadOnlyList<EntityChangedEventDto> Events, long LatestSequence)
    {
        this.Events = Events;
        this.LatestSequence = LatestSequence;
    }

    public required IReadOnlyList<EntityChangedEventDto> Events { get; init; }

    public required long LatestSequence { get; init; }
}
