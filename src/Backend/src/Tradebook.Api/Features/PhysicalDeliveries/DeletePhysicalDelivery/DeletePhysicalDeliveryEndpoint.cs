using FastEndpoints;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.PhysicalDeliveries.DeletePhysicalDelivery;

public sealed class DeletePhysicalDeliveryEndpoint(IDeliveryRepository repository, ICacheService cache) : Endpoint<DeletePhysicalDeliveryRequest>
{
    public override void Configure() { Delete("/api/v1/deliveries/{deliveryId}"); Policies("BackOfficePolicy"); }
    public override async Task HandleAsync(DeletePhysicalDeliveryRequest request, CancellationToken cancellationToken)
    {
        var outcome = await repository.CancelAtomicAsync(request.DeliveryId, request.Version, request.Reason, ActorId.From(User), cancellationToken);
        if (outcome == MutationOutcome.NotFound) { await SendNotFoundAsync(cancellationToken); return; }
        if (outcome == MutationOutcome.VersionConflict)
        {
            var current = await repository.GetByIdAsync(request.DeliveryId, cancellationToken);
            await SendAsync(current!, 409, cancellationToken); return;
        }
        await cache.RemoveAsync($"delivery:{request.DeliveryId}", cancellationToken);
        await cache.RemoveAsync("deliveries:list", cancellationToken);
        await SendNoContentAsync(cancellationToken);
    }
}
