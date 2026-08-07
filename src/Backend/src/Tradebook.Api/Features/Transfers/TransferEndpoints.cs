using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Transfers;

public sealed record GetTransferByIdRequest(Guid TransferId);

public sealed class CreateTransferEndpoint(ITransferRepository repository) : Endpoint<CreateTransferRequest, TransferDetailsDto>
{
    public override void Configure() { Post("/api/v1/transfers"); Policies("TraderPolicy"); }
    public override async Task HandleAsync(CreateTransferRequest request, CancellationToken ct) =>
        await SendAsync(await repository.CreateAtomicAsync(request, ActorId.From(User), ct), 201, ct);
}

public sealed class GetTransferByIdEndpoint(ITransferRepository repository) : Endpoint<GetTransferByIdRequest, TransferDetailsDto>
{
    public override void Configure() { Get("/api/v1/transfers/{transferId}"); Policies("ReadPolicy"); }
    public override async Task HandleAsync(GetTransferByIdRequest request, CancellationToken ct)
    {
        var result = await repository.GetByIdAsync(request.TransferId, ct);
        if (result is null) { await SendNotFoundAsync(ct); return; }
        await SendOkAsync(result, ct);
    }
}

public sealed class GetTransferHistoryEndpoint(ITransferRepository repository) : Endpoint<GetTransferHistoryRequest, GetTransferHistoryResponse>
{
    public override void Configure() { Get("/api/v1/transfers"); Policies("ReadPolicy"); }
    public override async Task HandleAsync(GetTransferHistoryRequest request, CancellationToken ct) => await SendOkAsync(await repository.GetHistoryAsync(request, ct), ct);
}

public sealed class UpdateTransferEndpoint(ITransferRepository repository) : Endpoint<UpdateTransferRequest, TransferDetailsDto>
{
    public override void Configure() { Put("/api/v1/transfers/{transferId}"); Policies("TraderPolicy"); }
    public override async Task HandleAsync(UpdateTransferRequest request, CancellationToken ct)
    {
        var result = await repository.UpdateAtomicAsync(request, ActorId.From(User), ct);
        if (result is not null) { await SendOkAsync(result, ct); return; }
        var current = await repository.GetByIdAsync(request.TransferId, ct);
        if (current is null) { await SendNotFoundAsync(ct); return; }
        await SendAsync(current, 409, ct);
    }
}

public sealed class CancelTransferEndpoint(ITransferRepository repository) : Endpoint<CancelTransferRequest>
{
    public override void Configure() { Delete("/api/v1/transfers/{transferId}"); Policies("BackOfficePolicy"); }
    public override async Task HandleAsync(CancelTransferRequest request, CancellationToken ct)
    {
        var outcome = await repository.CancelAtomicAsync(request.TransferId, request.Version, request.Reason, ActorId.From(User), ct);
        if (outcome == MutationOutcome.NotFound) { await SendNotFoundAsync(ct); return; }
        if (outcome == MutationOutcome.VersionConflict) { await SendAsync((await repository.GetByIdAsync(request.TransferId, ct))!, 409, ct); return; }
        await SendNoContentAsync(ct);
    }
}
