using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;

namespace Tradebook.Core.DTOs;

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

    public Quantity? VolumeRealisedMwh { get; init; }

    public string? Status { get; init; }

    public required long Version { get; init; }
}
