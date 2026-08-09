using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.CapacityBookings;

public sealed class DeleteCapacityBookingEndpoint(ICapacityBookingRepository repository)
    : Endpoint<DeleteCapacityBookingRequest>
{
    public override void Configure()
    {
        Delete("/api/v1/capacity-bookings/{capacityBookingId}");
        Policies("BackOfficePolicy");
    }

    public override async Task HandleAsync(
        DeleteCapacityBookingRequest request,
        CancellationToken ct
    )
    {
        var outcome = await (
            repository.DeleteAtomicAsync(
                request.CapacityBookingId,
                request.Version,
                request.Reason,
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
            await (
                Send.ResponseAsync(
                    (
                        await (
                            repository.GetByIdAsync(request.CapacityBookingId, ct)
                        ).ConfigureAwait(false)
                    )!,
                    409,
                    cancellation: ct
                )
            ).ConfigureAwait(false);
            return;
        }
        await (Send.NoContentAsync(ct)).ConfigureAwait(false);
    }
}
