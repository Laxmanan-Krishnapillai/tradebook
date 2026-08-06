using FastEndpoints;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.PhysicalDeliveries.UpdatePhysicalDelivery;

public sealed class UpdatePhysicalDeliveryEndpoint(IDeliveryRepository repository, ICacheService cache) : Endpoint<UpdatePhysicalDeliveryRequest, PhysicalDeliveryDetailsDto>
{
    public override void Configure() { Put("/api/v1/deliveries/{deliveryId}"); Policies("TraderPolicy"); }
    public override async Task HandleAsync(UpdatePhysicalDeliveryRequest request, CancellationToken cancellationToken)
    {
        var updated = await repository.UpdateAtomicAsync(request, ActorId.From(User), cancellationToken);
        if (updated is null)
        {
            var current = await repository.GetByIdAsync(request.DeliveryId, cancellationToken);
            if (current is null) { await SendNotFoundAsync(cancellationToken); return; }
            await SendAsync(current, 409, cancellationToken); return;
        }
        await cache.RemoveAsync($"delivery:{request.DeliveryId}", cancellationToken);
        await cache.RemoveAsync("deliveries:list", cancellationToken);
        await SendOkAsync(updated, cancellationToken);
    }
}
