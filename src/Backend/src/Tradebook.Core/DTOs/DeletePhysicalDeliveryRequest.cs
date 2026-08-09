using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;

namespace Tradebook.Core.DTOs;

public sealed record DeletePhysicalDeliveryRequest
{
    public DeletePhysicalDeliveryRequest() { }

    [SetsRequiredMembers]
    public DeletePhysicalDeliveryRequest(DeliveryId DeliveryId, string Reason, long Version)
    {
        this.DeliveryId = DeliveryId;
        this.Reason = Reason;
        this.Version = Version;
    }

    public required DeliveryId DeliveryId { get; init; }

    public required string Reason { get; init; }

    public required long Version { get; init; }
}
