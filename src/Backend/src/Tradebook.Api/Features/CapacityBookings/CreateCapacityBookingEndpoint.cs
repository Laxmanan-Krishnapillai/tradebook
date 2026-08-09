using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.CapacityBookings;

public sealed class CreateCapacityBookingEndpoint(ICapacityBookingRepository repository)
    : Endpoint<CreateCapacityBookingRequest, CapacityBookingDetailsDto>
{
    public override void Configure()
    {
        Post("/api/v1/capacity-bookings");
        Policies("TraderPolicy");
    }

    public override async Task HandleAsync(
        CreateCapacityBookingRequest request,
        CancellationToken ct
    ) =>
        await (
            Send.ResponseAsync(
                await (
                    repository.CreateAtomicAsync(request, ActorId.From(User), ct)
                ).ConfigureAwait(false),
                201,
                cancellation: ct
            )
        ).ConfigureAwait(false);
}
