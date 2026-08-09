using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.PhysicalDeliveries.UpdatePhysicalDelivery;

public sealed class UpdatePhysicalDeliveryEndpoint(
    IDeliveryRepository repository,
    ICacheService cache
) : Endpoint<UpdatePhysicalDeliveryRequest, PhysicalDeliveryDetailsDto>
{
    public override void Configure()
    {
        Put("/api/v1/deliveries/{deliveryId}");
        Policies("TraderPolicy");
    }

    public override async Task HandleAsync(UpdatePhysicalDeliveryRequest req, CancellationToken ct)
    {
        var updated = await (
            repository.UpdateAtomicAsync(req, ActorId.From(User), ct)
        ).ConfigureAwait(false);
        if (updated is null)
        {
            var current = await (repository.GetByIdAsync(req.DeliveryId, ct)).ConfigureAwait(false);
            if (current is null)
            {
                await (Send.NotFoundAsync(ct)).ConfigureAwait(false);
                return;
            }
            await (Send.ResponseAsync(current, 409, cancellation: ct)).ConfigureAwait(false);
            return;
        }
        await (cache.RemoveAsync($"delivery:{req.DeliveryId}", ct)).ConfigureAwait(false);
        await (cache.RemoveAsync("deliveries:list", ct)).ConfigureAwait(false);
        await (Send.OkAsync(updated, cancellation: ct)).ConfigureAwait(false);
    }
}
