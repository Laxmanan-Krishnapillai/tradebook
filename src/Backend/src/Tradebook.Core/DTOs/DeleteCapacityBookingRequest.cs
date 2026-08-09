using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record DeleteCapacityBookingRequest
{
    public DeleteCapacityBookingRequest() { }

    [SetsRequiredMembers]
    public DeleteCapacityBookingRequest(Guid CapacityBookingId, string Reason, long Version)
    {
        this.CapacityBookingId = CapacityBookingId;
        this.Reason = Reason;
        this.Version = Version;
    }

    public required Guid CapacityBookingId { get; init; }
    public required string Reason { get; init; }
    public required long Version { get; init; }
}
