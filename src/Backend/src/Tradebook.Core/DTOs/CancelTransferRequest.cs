using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;

namespace Tradebook.Core.DTOs;

public sealed record CancelTransferRequest
{
    public CancelTransferRequest() { }

    [SetsRequiredMembers]
    public CancelTransferRequest(TransferId TransferId, string Reason, long Version)
    {
        this.TransferId = TransferId;
        this.Reason = Reason;
        this.Version = Version;
    }

    public required TransferId TransferId { get; init; }
    public required string Reason { get; init; }
    public required long Version { get; init; }
}
