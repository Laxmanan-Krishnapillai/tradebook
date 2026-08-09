using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.CapacityBookings;

public sealed class GetCapacityBookingByIdEndpoint(ICapacityBookingRepository repository)
    : Endpoint<GetCapacityBookingByIdRequest, CapacityBookingDetailsDto>
{
    public override void Configure()
    {
        Get("/api/v1/capacity-bookings/{capacityBookingId}");
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(
        GetCapacityBookingByIdRequest request,
        CancellationToken ct
    )
    {
        var result = await (repository.GetByIdAsync(request.CapacityBookingId, ct)).ConfigureAwait(
            false
        );
        if (result is null)
        {
            await (Send.NotFoundAsync(ct)).ConfigureAwait(false);
            return;
        }
        await (Send.OkAsync(result, cancellation: ct)).ConfigureAwait(false);
    }
}
