using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.TaxTariffs;

public sealed record GetTaxTariffByIdRequest(Guid TaxTariffId);

public sealed class CreateTaxTariffEndpoint(ITaxTariffRepository repository) : Endpoint<CreateTaxTariffRequest, TaxTariffDetailsDto>
{
    public override void Configure() { Post("/api/v1/tax-tariffs"); Policies("AdminPolicy"); }
    public override async Task HandleAsync(CreateTaxTariffRequest request, CancellationToken ct) => await Send.ResponseAsync(await repository.CreateAtomicAsync(request, ActorId.From(User), ct), 201, cancellation: ct);
}

public sealed class GetTaxTariffByIdEndpoint(ITaxTariffRepository repository) : Endpoint<GetTaxTariffByIdRequest, TaxTariffDetailsDto>
{
    public override void Configure() { Get("/api/v1/tax-tariffs/{taxTariffId}"); Policies("ReadPolicy"); }
    public override async Task HandleAsync(GetTaxTariffByIdRequest request, CancellationToken ct)
    {
        var result = await repository.GetByIdAsync(request.TaxTariffId, ct);
        if (result is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(result, cancellation: ct);
    }
}

public sealed class GetTaxTariffHistoryEndpoint(ITaxTariffRepository repository) : Endpoint<GetTaxTariffHistoryRequest, GetTaxTariffHistoryResponse>
{
    public override void Configure() { Get("/api/v1/tax-tariffs"); Policies("ReadPolicy"); }
    public override async Task HandleAsync(GetTaxTariffHistoryRequest request, CancellationToken ct) => await Send.OkAsync(await repository.GetHistoryAsync(request, ct), cancellation: ct);
}

public sealed class UpdateTaxTariffEndpoint(ITaxTariffRepository repository) : Endpoint<UpdateTaxTariffRequest, TaxTariffDetailsDto>
{
    public override void Configure() { Put("/api/v1/tax-tariffs/{taxTariffId}"); Policies("AdminPolicy"); }
    public override async Task HandleAsync(UpdateTaxTariffRequest request, CancellationToken ct)
    {
        var result = await repository.UpdateAtomicAsync(request, ActorId.From(User), ct);
        if (result is not null) { await Send.OkAsync(result, cancellation: ct); return; }
        var current = await repository.GetByIdAsync(request.TaxTariffId, ct);
        if (current is null) { await Send.NotFoundAsync(ct); return; }
        await Send.ResponseAsync(current, 409, cancellation: ct);
    }
}

public sealed class DeleteTaxTariffEndpoint(ITaxTariffRepository repository) : Endpoint<DeleteTaxTariffRequest>
{
    public override void Configure() { Delete("/api/v1/tax-tariffs/{taxTariffId}"); Policies("AdminPolicy"); }
    public override async Task HandleAsync(DeleteTaxTariffRequest request, CancellationToken ct)
    {
        var outcome = await repository.DeleteAtomicAsync(request.TaxTariffId, request.Version, request.Reason, ActorId.From(User), ct);
        if (outcome == MutationOutcome.NotFound) { await Send.NotFoundAsync(ct); return; }
        if (outcome == MutationOutcome.VersionConflict) { await Send.ResponseAsync((await repository.GetByIdAsync(request.TaxTariffId, ct))!, 409, cancellation: ct); return; }
        await Send.NoContentAsync(ct);
    }
}
