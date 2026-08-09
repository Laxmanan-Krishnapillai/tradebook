using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.PhysicalDeliveries.CreatePhysicalDelivery;

public sealed class CreatePhysicalDeliveryEndpoint(
    IDeliveryRepository repository,
    ICacheService cache
) : Endpoint<CreatePhysicalDeliveryRequest, CreatePhysicalDeliveryResponse>
{
    public override void Configure()
    {
        Post("/api/v1/deliveries");
        Policies("TraderPolicy");
    }

    public override async Task HandleAsync(CreatePhysicalDeliveryRequest req, CancellationToken ct)
    {
        var actorId = ActorId.From(User);
        var delivery = await (repository.CreateAtomicAsync(req, actorId, ct)).ConfigureAwait(false);
        await (cache.RemoveAsync("deliveries:list", ct)).ConfigureAwait(false);
        await (
            Send.ResponseAsync(PhysicalDeliveryMapper.ToResponse(delivery), 201, cancellation: ct)
        ).ConfigureAwait(false);
    }
}
