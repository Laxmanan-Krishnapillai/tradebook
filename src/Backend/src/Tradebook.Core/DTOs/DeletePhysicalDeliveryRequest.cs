using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record DeletePhysicalDeliveryRequest
{
    public DeletePhysicalDeliveryRequest() { }

    [SetsRequiredMembers]
    public DeletePhysicalDeliveryRequest(Guid DeliveryId, string Reason, long Version)
    {
        this.DeliveryId = DeliveryId;
        this.Reason = Reason;
        this.Version = Version;
    }

    public required Guid DeliveryId { get; init; }

    public required string Reason { get; init; }

    public required long Version { get; init; }
}
