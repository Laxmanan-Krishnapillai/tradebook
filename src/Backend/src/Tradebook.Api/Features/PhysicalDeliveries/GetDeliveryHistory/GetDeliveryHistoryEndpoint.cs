using FastEndpoints;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.PhysicalDeliveries.GetDeliveryHistory;

public sealed class GetDeliveryHistoryEndpoint(IDeliveryRepository repository) : Endpoint<GetDeliveryHistoryRequest, GetDeliveryHistoryResponse>
{
    public override void Configure() { Get("/api/v1/deliveries"); Policies("ReadPolicy"); }
    public override async Task HandleAsync(GetDeliveryHistoryRequest request, CancellationToken cancellationToken) => await Send.OkAsync(await repository.GetHistoryAsync(request, cancellationToken), cancellation: cancellationToken);
}
