using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record UpdatePhysicalDeliveryRequest
{
    public UpdatePhysicalDeliveryRequest() { }

    [SetsRequiredMembers]
    public UpdatePhysicalDeliveryRequest(
        Guid DeliveryId,
        decimal? VolumeRealisedMwh,
        string? Status,
        long Version
    )
    {
        this.DeliveryId = DeliveryId;
        this.VolumeRealisedMwh = VolumeRealisedMwh;
        this.Status = Status;
        this.Version = Version;
    }

    public required Guid DeliveryId { get; init; }

    [TsOptional]
    public decimal? VolumeRealisedMwh { get; init; }

    [TsOptional]
    public string? Status { get; init; }

    public required long Version { get; init; }
}
