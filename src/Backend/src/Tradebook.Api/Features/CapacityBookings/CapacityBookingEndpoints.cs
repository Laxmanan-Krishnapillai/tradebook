using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.CapacityBookings;

public sealed record GetCapacityBookingByIdRequest(Guid CapacityBookingId);

public sealed class CreateCapacityBookingEndpoint(ICapacityBookingRepository repository)
    : Endpoint<CreateCapacityBookingRequest, CapacityBookingDetailsDto>
{
    public override void Configure() { Post("/api/v1/capacity-bookings"); Policies("TraderPolicy"); }
    public override async Task HandleAsync(CreateCapacityBookingRequest request, CancellationToken ct) =>
        await Send.ResponseAsync(await repository.CreateAtomicAsync(request, ActorId.From(User), ct), 201, cancellation: ct);
}

public sealed class GetCapacityBookingByIdEndpoint(ICapacityBookingRepository repository)
    : Endpoint<GetCapacityBookingByIdRequest, CapacityBookingDetailsDto>
{
    public override void Configure() { Get("/api/v1/capacity-bookings/{capacityBookingId}"); Policies("ReadPolicy"); }
    public override async Task HandleAsync(GetCapacityBookingByIdRequest request, CancellationToken ct)
    {
        var result = await repository.GetByIdAsync(request.CapacityBookingId, ct);
        if (result is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(result, cancellation: ct);
    }
}

public sealed class GetCapacityBookingHistoryEndpoint(ICapacityBookingRepository repository)
    : Endpoint<GetCapacityBookingHistoryRequest, GetCapacityBookingHistoryResponse>
{
    public override void Configure() { Get("/api/v1/capacity-bookings"); Policies("ReadPolicy"); }
    public override async Task HandleAsync(GetCapacityBookingHistoryRequest request, CancellationToken ct) =>
        await Send.OkAsync(await repository.GetHistoryAsync(request, ct), cancellation: ct);
}

public sealed class UpdateCapacityBookingEndpoint(ICapacityBookingRepository repository)
    : Endpoint<UpdateCapacityBookingRequest, CapacityBookingDetailsDto>
{
    public override void Configure() { Put("/api/v1/capacity-bookings/{capacityBookingId}"); Policies("TraderPolicy"); }
    public override async Task HandleAsync(UpdateCapacityBookingRequest request, CancellationToken ct)
    {
        var result = await repository.UpdateAtomicAsync(request, ActorId.From(User), ct);
        if (result is not null) { await Send.OkAsync(result, cancellation: ct); return; }
        var current = await repository.GetByIdAsync(request.CapacityBookingId, ct);
        if (current is null) { await Send.NotFoundAsync(ct); return; }
        await Send.ResponseAsync(current, 409, cancellation: ct);
    }
}

public sealed class DeleteCapacityBookingEndpoint(ICapacityBookingRepository repository)
    : Endpoint<DeleteCapacityBookingRequest>
{
    public override void Configure() { Delete("/api/v1/capacity-bookings/{capacityBookingId}"); Policies("BackOfficePolicy"); }
    public override async Task HandleAsync(DeleteCapacityBookingRequest request, CancellationToken ct)
    {
        var outcome = await repository.DeleteAtomicAsync(request.CapacityBookingId, request.Version, request.Reason, ActorId.From(User), ct);
        if (outcome == MutationOutcome.NotFound) { await Send.NotFoundAsync(ct); return; }
        if (outcome == MutationOutcome.VersionConflict) { await Send.ResponseAsync((await repository.GetByIdAsync(request.CapacityBookingId, ct))!, 409, cancellation: ct); return; }
        await Send.NoContentAsync(ct);
    }
}
