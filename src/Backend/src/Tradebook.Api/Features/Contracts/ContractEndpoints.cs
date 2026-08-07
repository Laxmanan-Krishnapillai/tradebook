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
        await SendAsync(await repository.CreateAtomicAsync(request, ActorId.From(User), ct), 201, ct);
}

public sealed class GetContractByIdEndpoint(IContractRepository repository)
    : Endpoint<GetContractByIdRequest, ContractDetailsDto>
{
    public override void Configure() { Get("/api/v1/contracts/{contractId}"); Policies("ReadPolicy"); }
    public override async Task HandleAsync(GetContractByIdRequest request, CancellationToken ct)
    {
        var result = await repository.GetByIdAsync(request.ContractId, ct);
        if (result is null) { await SendNotFoundAsync(ct); return; }
        await SendOkAsync(result, ct);
    }
}

public sealed class GetContractHistoryEndpoint(IContractRepository repository)
    : Endpoint<GetContractHistoryRequest, GetContractHistoryResponse>
{
    public override void Configure() { Get("/api/v1/contracts"); Policies("ReadPolicy"); }
    public override async Task HandleAsync(GetContractHistoryRequest request, CancellationToken ct) =>
        await SendOkAsync(await repository.GetHistoryAsync(request, ct), ct);
}

public sealed class UpdateContractEndpoint(IContractRepository repository)
    : Endpoint<UpdateContractRequest, ContractDetailsDto>
{
    public override void Configure() { Put("/api/v1/contracts/{contractId}"); Policies("TraderPolicy"); }
    public override async Task HandleAsync(UpdateContractRequest request, CancellationToken ct)
    {
        var result = await repository.UpdateAtomicAsync(request, ActorId.From(User), ct);
        if (result is not null) { await SendOkAsync(result, ct); return; }
        var current = await repository.GetByIdAsync(request.ContractId, ct);
        if (current is null) { await SendNotFoundAsync(ct); return; }
        await SendAsync(current, 409, ct);
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
        if (outcome == MutationOutcome.NotFound) { await SendNotFoundAsync(ct); return; }
        if (outcome == MutationOutcome.VersionConflict)
        {
            await SendAsync((await repository.GetByIdAsync(request.ContractId, ct))!, 409, ct);
            return;
        }
        await SendNoContentAsync(ct);
    }
}
