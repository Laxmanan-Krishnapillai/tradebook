using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;

namespace Tradebook.Core.DTOs;

public sealed record DeleteHedgeRequest
{
    public DeleteHedgeRequest() { }

    [SetsRequiredMembers]
    public DeleteHedgeRequest(HedgeId HedgeId, string Reason, long Version)
    {
        this.HedgeId = HedgeId;
        this.Reason = Reason;
        this.Version = Version;
    }

    public required HedgeId HedgeId { get; init; }

    public required string Reason { get; init; }

    public required long Version { get; init; }
}
