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
        await SendAsync(await repository.CreateAtomicAsync(request, ActorId.From(User), ct), 201, ct);
}

public sealed class GetCapacityBookingByIdEndpoint(ICapacityBookingRepository repository)
    : Endpoint<GetCapacityBookingByIdRequest, CapacityBookingDetailsDto>
{
    public override void Configure() { Get("/api/v1/capacity-bookings/{capacityBookingId}"); Policies("ReadPolicy"); }
    public override async Task HandleAsync(GetCapacityBookingByIdRequest request, CancellationToken ct)
    {
        var result = await repository.GetByIdAsync(request.CapacityBookingId, ct);
        if (result is null) { await SendNotFoundAsync(ct); return; }
        await SendOkAsync(result, ct);
    }
}

public sealed class GetCapacityBookingHistoryEndpoint(ICapacityBookingRepository repository)
    : Endpoint<GetCapacityBookingHistoryRequest, GetCapacityBookingHistoryResponse>
{
    public override void Configure() { Get("/api/v1/capacity-bookings"); Policies("ReadPolicy"); }
    public override async Task HandleAsync(GetCapacityBookingHistoryRequest request, CancellationToken ct) =>
        await SendOkAsync(await repository.GetHistoryAsync(request, ct), ct);
}

public sealed class UpdateCapacityBookingEndpoint(ICapacityBookingRepository repository)
    : Endpoint<UpdateCapacityBookingRequest, CapacityBookingDetailsDto>
{
    public override void Configure() { Put("/api/v1/capacity-bookings/{capacityBookingId}"); Policies("TraderPolicy"); }
    public override async Task HandleAsync(UpdateCapacityBookingRequest request, CancellationToken ct)
    {
        var result = await repository.UpdateAtomicAsync(request, ActorId.From(User), ct);
        if (result is not null) { await SendOkAsync(result, ct); return; }
        var current = await repository.GetByIdAsync(request.CapacityBookingId, ct);
        if (current is null) { await SendNotFoundAsync(ct); return; }
        await SendAsync(current, 409, ct);
    }
}

public sealed class DeleteCapacityBookingEndpoint(ICapacityBookingRepository repository)
    : Endpoint<DeleteCapacityBookingRequest>
{
    public override void Configure() { Delete("/api/v1/capacity-bookings/{capacityBookingId}"); Policies("BackOfficePolicy"); }
    public override async Task HandleAsync(DeleteCapacityBookingRequest request, CancellationToken ct)
    {
        var outcome = await repository.DeleteAtomicAsync(request.CapacityBookingId, request.Version, request.Reason, ActorId.From(User), ct);
        if (outcome == MutationOutcome.NotFound) { await SendNotFoundAsync(ct); return; }
        if (outcome == MutationOutcome.VersionConflict) { await SendAsync((await repository.GetByIdAsync(request.CapacityBookingId, ct))!, 409, ct); return; }
        await SendNoContentAsync(ct);
    }
}
