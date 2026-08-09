using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record DeleteCapacityBookingRequest
{
    public DeleteCapacityBookingRequest() { }

    [SetsRequiredMembers]
    public DeleteCapacityBookingRequest(
        CapacityBookingId CapacityBookingId,
        string Reason,
        long Version
    )
    {
        this.CapacityBookingId = CapacityBookingId;
        this.Reason = Reason;
        this.Version = Version;
    }

    public required CapacityBookingId CapacityBookingId { get; init; }
    public required string Reason { get; init; }
    public required long Version { get; init; }
}
