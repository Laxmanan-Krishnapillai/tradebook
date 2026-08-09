using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record UpdatePhysicalDeliveryRequest
{
    public UpdatePhysicalDeliveryRequest() { }

    [SetsRequiredMembers]
    public UpdatePhysicalDeliveryRequest(
        DeliveryId DeliveryId,
        Quantity? VolumeRealisedMwh,
        string? Status,
        long Version
    )
    {
        this.DeliveryId = DeliveryId;
        this.VolumeRealisedMwh = VolumeRealisedMwh;
        this.Status = Status;
        this.Version = Version;
    }

    public required DeliveryId DeliveryId { get; init; }

    [TsOptional]
    public Quantity? VolumeRealisedMwh { get; init; }

    [TsOptional]
    public string? Status { get; init; }

    public required long Version { get; init; }
}
