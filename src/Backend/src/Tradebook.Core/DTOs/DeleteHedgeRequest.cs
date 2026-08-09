using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record DeleteHedgeRequest
{
    public DeleteHedgeRequest() { }

    [SetsRequiredMembers]
    public DeleteHedgeRequest(Guid HedgeId, string Reason, long Version)
    {
        this.HedgeId = HedgeId;
        this.Reason = Reason;
        this.Version = Version;
    }

    public required Guid HedgeId { get; init; }

    public required string Reason { get; init; }

    public required long Version { get; init; }
}
