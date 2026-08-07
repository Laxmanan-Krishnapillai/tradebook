using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.GooCertificates;

public sealed record GetGooCertificateByIdRequest(Guid GooCertificateTransactionId);

public sealed class CreateGooCertificateEndpoint(IGooCertificateRepository repository) : Endpoint<CreateGooCertificateTransactionRequest, GooCertificateTransactionDetailsDto>
{
    public override void Configure() { Post("/api/v1/goo-certificates"); Policies("TraderPolicy"); }
    public override async Task HandleAsync(CreateGooCertificateTransactionRequest request, CancellationToken ct) => await SendAsync(await repository.CreateAtomicAsync(request, ActorId.From(User), ct), 201, ct);
}

public sealed class GetGooCertificateByIdEndpoint(IGooCertificateRepository repository) : Endpoint<GetGooCertificateByIdRequest, GooCertificateTransactionDetailsDto>
{
    public override void Configure() { Get("/api/v1/goo-certificates/{gooCertificateTransactionId}"); Policies("ReadPolicy"); }
    public override async Task HandleAsync(GetGooCertificateByIdRequest request, CancellationToken ct)
    {
        var result = await repository.GetByIdAsync(request.GooCertificateTransactionId, ct);
        if (result is null) { await SendNotFoundAsync(ct); return; }
        await SendOkAsync(result, ct);
    }
}

public sealed class GetGooCertificateHistoryEndpoint(IGooCertificateRepository repository) : Endpoint<GetGooCertificateHistoryRequest, GetGooCertificateHistoryResponse>
{
    public override void Configure() { Get("/api/v1/goo-certificates"); Policies("ReadPolicy"); }
    public override async Task HandleAsync(GetGooCertificateHistoryRequest request, CancellationToken ct) => await SendOkAsync(await repository.GetHistoryAsync(request, ct), ct);
}

public sealed class UpdateGooCertificateEndpoint(IGooCertificateRepository repository) : Endpoint<UpdateGooCertificateTransactionRequest, GooCertificateTransactionDetailsDto>
{
    public override void Configure() { Put("/api/v1/goo-certificates/{gooCertificateTransactionId}"); Policies("TraderPolicy"); }
    public override async Task HandleAsync(UpdateGooCertificateTransactionRequest request, CancellationToken ct)
    {
        var result = await repository.UpdateAtomicAsync(request, ActorId.From(User), ct);
        if (result is not null) { await SendOkAsync(result, ct); return; }
        var current = await repository.GetByIdAsync(request.GooCertificateTransactionId, ct);
        if (current is null) { await SendNotFoundAsync(ct); return; }
        await SendAsync(current, 409, ct);
    }
}

public sealed class RequestGooBatchExportEndpoint(IGooCertificateRepository repository) : Endpoint<RequestGooBatchExportRequest, GooCertificateTransactionDetailsDto>
{
    public override void Configure() { Post("/api/v1/goo-certificates/{gooCertificateTransactionId}/request-batch-export"); Policies("BackOfficePolicy"); }
    public override async Task HandleAsync(RequestGooBatchExportRequest request, CancellationToken ct)
    {
        var result = await repository.RequestBatchExportAtomicAsync(request.GooCertificateTransactionId, request.Version, ActorId.From(User), ct);
        if (result is not null) { await SendOkAsync(result, ct); return; }
        var current = await repository.GetByIdAsync(request.GooCertificateTransactionId, ct);
        if (current is null) { await SendNotFoundAsync(ct); return; }
        await SendAsync(current, 409, ct);
    }
}

public sealed class DeleteGooCertificateEndpoint(IGooCertificateRepository repository) : Endpoint<DeleteGooCertificateTransactionRequest>
{
    public override void Configure() { Delete("/api/v1/goo-certificates/{gooCertificateTransactionId}"); Policies("BackOfficePolicy"); }
    public override async Task HandleAsync(DeleteGooCertificateTransactionRequest request, CancellationToken ct)
    {
        var outcome = await repository.DeleteAtomicAsync(request.GooCertificateTransactionId, request.Version, request.Reason, ActorId.From(User), ct);
        if (outcome == MutationOutcome.NotFound) { await SendNotFoundAsync(ct); return; }
        if (outcome == MutationOutcome.VersionConflict) { await SendAsync((await repository.GetByIdAsync(request.GooCertificateTransactionId, ct))!, 409, ct); return; }
        await SendNoContentAsync(ct);
    }
}
