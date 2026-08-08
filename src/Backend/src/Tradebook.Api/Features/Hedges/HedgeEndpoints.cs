using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Hedges;

public sealed record GetHedgeByIdRequest(Guid HedgeId);

public sealed class CreateHedgeEndpoint(IHedgeRepository repository) : Endpoint<CreateHedgeRequest, HedgeDetailsDto>
{
    public override void Configure() { Post("/api/v1/hedges"); Policies("TraderPolicy"); }

    public override async Task HandleAsync(CreateHedgeRequest request, CancellationToken ct) =>
        await Send.ResponseAsync(await repository.CreateAtomicAsync(request, ActorId.From(User), ct), 201, cancellation: ct);
}

public sealed class GetHedgeByIdEndpoint(IHedgeRepository repository) : Endpoint<GetHedgeByIdRequest, HedgeDetailsDto>
{
    public override void Configure() { Get("/api/v1/hedges/{hedgeId}"); Policies("ReadPolicy"); }

    public override async Task HandleAsync(GetHedgeByIdRequest request, CancellationToken ct)
    {
        var result = await repository.GetByIdAsync(request.HedgeId, ct);
        if (result is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(result, cancellation: ct);
    }
}

public sealed class GetHedgeHistoryEndpoint(IHedgeRepository repository) : Endpoint<GetHedgeHistoryRequest, GetHedgeHistoryResponse>
{
    public override void Configure() { Get("/api/v1/hedges"); Policies("ReadPolicy"); }

    public override async Task HandleAsync(GetHedgeHistoryRequest request, CancellationToken ct) =>
        await Send.OkAsync(await repository.GetHistoryAsync(request, ct), cancellation: ct);
}

public sealed class UpdateHedgeEndpoint(IHedgeRepository repository) : Endpoint<UpdateHedgeRequest, HedgeDetailsDto>
{
    public override void Configure() { Put("/api/v1/hedges/{hedgeId}"); Policies("TraderPolicy"); }

    public override async Task HandleAsync(UpdateHedgeRequest request, CancellationToken ct)
    {
        var result = await repository.UpdateAtomicAsync(request, ActorId.From(User), ct);
        if (result is not null) { await Send.OkAsync(result, cancellation: ct); return; }
        var current = await repository.GetByIdAsync(request.HedgeId, ct);
        if (current is null) { await Send.NotFoundAsync(ct); return; }
        await Send.ResponseAsync(current, 409, cancellation: ct);
    }
}

public sealed class DeleteHedgeEndpoint(IHedgeRepository repository) : Endpoint<DeleteHedgeRequest>
{
    public override void Configure() { Delete("/api/v1/hedges/{hedgeId}"); Policies("BackOfficePolicy"); }

    public override async Task HandleAsync(DeleteHedgeRequest request, CancellationToken ct)
    {
        var outcome = await repository.DeleteAtomicAsync(request.HedgeId, request.Version, request.Reason, ActorId.From(User), ct);
        if (outcome == MutationOutcome.NotFound) { await Send.NotFoundAsync(ct); return; }
        if (outcome == MutationOutcome.VersionConflict)
        {
            await Send.ResponseAsync((await repository.GetByIdAsync(request.HedgeId, ct))!, 409, cancellation: ct);
            return;
        }
        await Send.NoContentAsync(ct);
    }
}
