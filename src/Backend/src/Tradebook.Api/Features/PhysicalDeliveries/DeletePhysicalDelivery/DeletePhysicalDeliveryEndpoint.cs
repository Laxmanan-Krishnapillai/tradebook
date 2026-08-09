using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.PhysicalDeliveries.DeletePhysicalDelivery;

public sealed class DeletePhysicalDeliveryEndpoint(
    IDeliveryRepository repository,
    ICacheService cache
) : Endpoint<DeletePhysicalDeliveryRequest>
{
    public override void Configure()
    {
        Delete("/api/v1/deliveries/{deliveryId}");
        Policies("BackOfficePolicy");
    }

    public override async Task HandleAsync(DeletePhysicalDeliveryRequest req, CancellationToken ct)
    {
        var outcome = await (
            repository.CancelAtomicAsync(
                req.DeliveryId,
                req.Version,
                req.Reason,
                ActorId.From(User),
                ct
            )
        ).ConfigureAwait(false);
        if (outcome == MutationOutcome.NotFound)
        {
            await (Send.NotFoundAsync(ct)).ConfigureAwait(false);
            return;
        }
        if (outcome == MutationOutcome.VersionConflict)
        {
            var current = await (repository.GetByIdAsync(req.DeliveryId, ct)).ConfigureAwait(false);
            await (Send.ResponseAsync(current!, 409, cancellation: ct)).ConfigureAwait(false);
            return;
        }
        await (cache.RemoveAsync($"delivery:{req.DeliveryId}", ct)).ConfigureAwait(false);
        await (cache.RemoveAsync("deliveries:list", ct)).ConfigureAwait(false);
        await (Send.NoContentAsync(ct)).ConfigureAwait(false);
    }
}
