using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.PhysicalDeliveries.GetDeliveryById;

public sealed class GetDeliveryByIdEndpoint(IDeliveryRepository repository, ICacheService cache)
    : Endpoint<GetDeliveryByIdRequest, PhysicalDeliveryDetailsDto>
{
    public override void Configure()
    {
        Get("/api/v1/deliveries/{deliveryId}");
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(GetDeliveryByIdRequest req, CancellationToken ct)
    {
        var delivery = await (
            cache.GetOrCreateAsync(
                $"delivery:{req.DeliveryId}",
                token => new ValueTask<PhysicalDeliveryDetailsDto?>(
                    repository.GetByIdAsync(req.DeliveryId, token)
                ),
                TimeSpan.FromMinutes(5),
                ct
            )
        ).ConfigureAwait(false);
        if (delivery is null)
        {
            await (Send.NotFoundAsync(ct)).ConfigureAwait(false);
            return;
        }
        await (Send.OkAsync(delivery, cancellation: ct)).ConfigureAwait(false);
    }
}
