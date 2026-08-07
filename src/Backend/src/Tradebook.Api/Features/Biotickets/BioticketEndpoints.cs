using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Biotickets;

public sealed record GetBioticketByIdRequest(Guid BioticketId);

public sealed class CreateBioticketEndpoint(IBioticketRepository repository) : Endpoint<CreateBioticketRequest, BioticketDetailsDto>
{
    public override void Configure() { Post("/api/v1/biotickets"); Policies("TraderPolicy"); }
    public override async Task HandleAsync(CreateBioticketRequest request, CancellationToken ct) => await SendAsync(await repository.CreateAtomicAsync(request, ActorId.From(User), ct), 201, ct);
}

public sealed class GetBioticketByIdEndpoint(IBioticketRepository repository) : Endpoint<GetBioticketByIdRequest, BioticketDetailsDto>
{
    public override void Configure() { Get("/api/v1/biotickets/{bioticketId}"); Policies("ReadPolicy"); }
    public override async Task HandleAsync(GetBioticketByIdRequest request, CancellationToken ct)
    {
        var result = await repository.GetByIdAsync(request.BioticketId, ct);
        if (result is null) { await SendNotFoundAsync(ct); return; }
        await SendOkAsync(result, ct);
    }
}

public sealed class GetBioticketHistoryEndpoint(IBioticketRepository repository) : Endpoint<GetBioticketHistoryRequest, GetBioticketHistoryResponse>
{
    public override void Configure() { Get("/api/v1/biotickets"); Policies("ReadPolicy"); }
    public override async Task HandleAsync(GetBioticketHistoryRequest request, CancellationToken ct) => await SendOkAsync(await repository.GetHistoryAsync(request, ct), ct);
}

public sealed class UpdateBioticketEndpoint(IBioticketRepository repository) : Endpoint<UpdateBioticketRequest, BioticketDetailsDto>
{
    public override void Configure() { Put("/api/v1/biotickets/{bioticketId}"); Policies("TraderPolicy"); }
    public override async Task HandleAsync(UpdateBioticketRequest request, CancellationToken ct)
    {
        var result = await repository.UpdateAtomicAsync(request, ActorId.From(User), ct);
        if (result is not null) { await SendOkAsync(result, ct); return; }
        var current = await repository.GetByIdAsync(request.BioticketId, ct);
        if (current is null) { await SendNotFoundAsync(ct); return; }
        await SendAsync(current, 409, ct);
    }
}

public sealed class CancelBioticketEndpoint(IBioticketRepository repository) : Endpoint<CancelBioticketRequest>
{
    public override void Configure() { Delete("/api/v1/biotickets/{bioticketId}"); Policies("BackOfficePolicy"); }
    public override async Task HandleAsync(CancelBioticketRequest request, CancellationToken ct)
    {
        var outcome = await repository.CancelAtomicAsync(request.BioticketId, request.Version, request.Reason, ActorId.From(User), ct);
        if (outcome == MutationOutcome.NotFound) { await SendNotFoundAsync(ct); return; }
        if (outcome == MutationOutcome.VersionConflict) { await SendAsync((await repository.GetByIdAsync(request.BioticketId, ct))!, 409, ct); return; }
        await SendNoContentAsync(ct);
    }
}
