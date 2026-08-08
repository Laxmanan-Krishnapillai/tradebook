using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Contracts;

public sealed record GetContractByIdRequest(Guid ContractId);

public sealed class CreateContractEndpoint(IContractRepository repository)
    : Endpoint<CreateContractRequest, ContractDetailsDto>
{
    public override void Configure() { Post("/api/v1/contracts"); Policies("TraderPolicy"); }
    public override async Task HandleAsync(CreateContractRequest request, CancellationToken ct) =>
        await Send.ResponseAsync(await repository.CreateAtomicAsync(request, ActorId.From(User), ct), 201, cancellation: ct);
}

public sealed class GetContractByIdEndpoint(IContractRepository repository)
    : Endpoint<GetContractByIdRequest, ContractDetailsDto>
{
    public override void Configure() { Get("/api/v1/contracts/{contractId}"); Policies("ReadPolicy"); }
    public override async Task HandleAsync(GetContractByIdRequest request, CancellationToken ct)
    {
        var result = await repository.GetByIdAsync(request.ContractId, ct);
        if (result is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(result, cancellation: ct);
    }
}

public sealed class GetContractHistoryEndpoint(IContractRepository repository)
    : Endpoint<GetContractHistoryRequest, GetContractHistoryResponse>
{
    public override void Configure() { Get("/api/v1/contracts"); Policies("ReadPolicy"); }
    public override async Task HandleAsync(GetContractHistoryRequest request, CancellationToken ct) =>
        await Send.OkAsync(await repository.GetHistoryAsync(request, ct), cancellation: ct);
}

public sealed class UpdateContractEndpoint(IContractRepository repository)
    : Endpoint<UpdateContractRequest, ContractDetailsDto>
{
    public override void Configure() { Put("/api/v1/contracts/{contractId}"); Policies("TraderPolicy"); }
    public override async Task HandleAsync(UpdateContractRequest request, CancellationToken ct)
    {
        var result = await repository.UpdateAtomicAsync(request, ActorId.From(User), ct);
        if (result is not null) { await Send.OkAsync(result, cancellation: ct); return; }
        var current = await repository.GetByIdAsync(request.ContractId, ct);
        if (current is null) { await Send.NotFoundAsync(ct); return; }
        await Send.ResponseAsync(current, 409, cancellation: ct);
    }
}

public sealed class DeactivateContractEndpoint(IContractRepository repository)
    : Endpoint<DeactivateContractRequest>
{
    public override void Configure() { Delete("/api/v1/contracts/{contractId}"); Policies("BackOfficePolicy"); }
    public override async Task HandleAsync(DeactivateContractRequest request, CancellationToken ct)
    {
        var outcome = await repository.DeactivateAtomicAsync(
            request.ContractId, request.Version, request.Reason, ActorId.From(User), ct);
        if (outcome == MutationOutcome.NotFound) { await Send.NotFoundAsync(ct); return; }
        if (outcome == MutationOutcome.VersionConflict)
        {
            await Send.ResponseAsync((await repository.GetByIdAsync(request.ContractId, ct))!, 409, cancellation: ct);
            return;
        }
        await Send.NoContentAsync(ct);
    }
}
