using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.TaxTariffs;

public sealed record GetTaxTariffByIdRequest(Guid TaxTariffId);

public sealed class CreateTaxTariffEndpoint(ITaxTariffRepository repository) : Endpoint<CreateTaxTariffRequest, TaxTariffDetailsDto>
{
    public override void Configure() { Post("/api/v1/tax-tariffs"); Policies("AdminPolicy"); }
    public override async Task HandleAsync(CreateTaxTariffRequest request, CancellationToken ct) => await SendAsync(await repository.CreateAtomicAsync(request, ActorId.From(User), ct), 201, ct);
}

public sealed class GetTaxTariffByIdEndpoint(ITaxTariffRepository repository) : Endpoint<GetTaxTariffByIdRequest, TaxTariffDetailsDto>
{
    public override void Configure() { Get("/api/v1/tax-tariffs/{taxTariffId}"); Policies("ReadPolicy"); }
    public override async Task HandleAsync(GetTaxTariffByIdRequest request, CancellationToken ct)
    {
        var result = await repository.GetByIdAsync(request.TaxTariffId, ct);
        if (result is null) { await SendNotFoundAsync(ct); return; }
        await SendOkAsync(result, ct);
    }
}

public sealed class GetTaxTariffHistoryEndpoint(ITaxTariffRepository repository) : Endpoint<GetTaxTariffHistoryRequest, GetTaxTariffHistoryResponse>
{
    public override void Configure() { Get("/api/v1/tax-tariffs"); Policies("ReadPolicy"); }
    public override async Task HandleAsync(GetTaxTariffHistoryRequest request, CancellationToken ct) => await SendOkAsync(await repository.GetHistoryAsync(request, ct), ct);
}

public sealed class UpdateTaxTariffEndpoint(ITaxTariffRepository repository) : Endpoint<UpdateTaxTariffRequest, TaxTariffDetailsDto>
{
    public override void Configure() { Put("/api/v1/tax-tariffs/{taxTariffId}"); Policies("AdminPolicy"); }
    public override async Task HandleAsync(UpdateTaxTariffRequest request, CancellationToken ct)
    {
        var result = await repository.UpdateAtomicAsync(request, ActorId.From(User), ct);
        if (result is not null) { await SendOkAsync(result, ct); return; }
        var current = await repository.GetByIdAsync(request.TaxTariffId, ct);
        if (current is null) { await SendNotFoundAsync(ct); return; }
        await SendAsync(current, 409, ct);
    }
}

public sealed class DeleteTaxTariffEndpoint(ITaxTariffRepository repository) : Endpoint<DeleteTaxTariffRequest>
{
    public override void Configure() { Delete("/api/v1/tax-tariffs/{taxTariffId}"); Policies("AdminPolicy"); }
    public override async Task HandleAsync(DeleteTaxTariffRequest request, CancellationToken ct)
    {
        var outcome = await repository.DeleteAtomicAsync(request.TaxTariffId, request.Version, request.Reason, ActorId.From(User), ct);
        if (outcome == MutationOutcome.NotFound) { await SendNotFoundAsync(ct); return; }
        if (outcome == MutationOutcome.VersionConflict) { await SendAsync((await repository.GetByIdAsync(request.TaxTariffId, ct))!, 409, ct); return; }
        await SendNoContentAsync(ct);
    }
}
