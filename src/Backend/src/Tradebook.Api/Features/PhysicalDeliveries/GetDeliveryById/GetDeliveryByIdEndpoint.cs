using FastEndpoints;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.PhysicalDeliveries.GetDeliveryById;

public sealed record GetDeliveryByIdRequest(Guid DeliveryId);

public sealed class GetDeliveryByIdEndpoint(IDeliveryRepository repository, ICacheService cache) : Endpoint<GetDeliveryByIdRequest, PhysicalDeliveryDetailsDto>
{
    public override void Configure() { Get("/api/v1/deliveries/{deliveryId}"); Policies("ReadPolicy"); }
    public override async Task HandleAsync(GetDeliveryByIdRequest request, CancellationToken cancellationToken)
    {
        var delivery = await cache.GetOrCreateAsync($"delivery:{request.DeliveryId}", token => new ValueTask<PhysicalDeliveryDetailsDto?>(repository.GetByIdAsync(request.DeliveryId, token)), TimeSpan.FromMinutes(5), cancellationToken);
        if (delivery is null) { await Send.NotFoundAsync(cancellationToken); return; }
        await Send.OkAsync(delivery, cancellation: cancellationToken);
    }
}
