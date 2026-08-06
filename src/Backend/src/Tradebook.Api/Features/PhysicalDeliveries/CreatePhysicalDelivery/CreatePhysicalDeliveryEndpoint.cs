using FastEndpoints;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.PhysicalDeliveries.CreatePhysicalDelivery;

public sealed class CreatePhysicalDeliveryEndpoint(IDeliveryRepository repository, ICacheService cache) : Endpoint<CreatePhysicalDeliveryRequest, CreatePhysicalDeliveryResponse>
{
    public override void Configure() { Post("/api/v1/deliveries"); Policies("TraderPolicy"); }
    public override async Task HandleAsync(CreatePhysicalDeliveryRequest request, CancellationToken cancellationToken)
    {
        var actorId = ActorId.From(User);
        var delivery = await repository.CreateAtomicAsync(request, actorId, cancellationToken);
        await cache.RemoveAsync("deliveries:list", cancellationToken);
        await SendAsync(new CreatePhysicalDeliveryResponse(delivery.DeliveryId, delivery.ContractInstanceId, delivery.InvoiceAmountEur, delivery.Status, delivery.Version, delivery.CreatedAt), 201, cancellationToken);
    }
}
