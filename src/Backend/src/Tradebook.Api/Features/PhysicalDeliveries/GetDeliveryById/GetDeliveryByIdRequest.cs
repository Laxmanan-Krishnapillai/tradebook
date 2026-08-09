using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.PhysicalDeliveries.GetDeliveryById;

public sealed record GetDeliveryByIdRequest
{
    public GetDeliveryByIdRequest() { }

    [SetsRequiredMembers]
    public GetDeliveryByIdRequest(Guid DeliveryId) => this.DeliveryId = DeliveryId;

    public required Guid DeliveryId { get; init; }
}
