using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CancelTransferRequest
{
    public CancelTransferRequest() { }

    [SetsRequiredMembers]
    public CancelTransferRequest(Guid TransferId, string Reason, long Version)
    {
        this.TransferId = TransferId;
        this.Reason = Reason;
        this.Version = Version;
    }

    public required Guid TransferId { get; init; }
    public required string Reason { get; init; }
    public required long Version { get; init; }
}
