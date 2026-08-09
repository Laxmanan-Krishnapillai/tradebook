using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.CapacityBookings;

public sealed class GetCapacityBookingHistoryEndpoint(ICapacityBookingRepository repository)
    : Endpoint<GetCapacityBookingHistoryRequest, GetCapacityBookingHistoryResponse>
{
    public override void Configure()
    {
        Get("/api/v1/capacity-bookings");
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(
        GetCapacityBookingHistoryRequest request,
        CancellationToken ct
    ) =>
        await (
            Send.OkAsync(
                await (repository.GetHistoryAsync(request, ct)).ConfigureAwait(false),
                cancellation: ct
            )
        ).ConfigureAwait(false);
}
