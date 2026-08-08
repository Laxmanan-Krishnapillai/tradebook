using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.MarketPrices;

public sealed record GetMarketPriceByDateRequest(DateOnly PriceDate);

public sealed class UpsertMarketPriceEndpoint(IMarketPriceRepository repository) : Endpoint<UpsertMarketPriceRequest, MarketPriceDetailsDto>
{
    public override void Configure() { Put("/api/v1/market-prices/{priceDate}"); Policies("AdminPolicy"); }
    public override async Task HandleAsync(UpsertMarketPriceRequest request, CancellationToken ct)
    {
        var result = await repository.UpsertAtomicAsync(request, ActorId.From(User), ct);
        if (result is not null) { await Send.OkAsync(result, cancellation: ct); return; }
        var current = await repository.GetByDateAsync(request.PriceDate, ct);
        if (current is null) { await Send.NotFoundAsync(ct); return; }
        await Send.ResponseAsync(current, 409, cancellation: ct);
    }
}

public sealed class GetMarketPriceByDateEndpoint(IMarketPriceRepository repository) : Endpoint<GetMarketPriceByDateRequest, MarketPriceDetailsDto>
{
    public override void Configure() { Get("/api/v1/market-prices/{priceDate}"); Policies("ReadPolicy"); }
    public override async Task HandleAsync(GetMarketPriceByDateRequest request, CancellationToken ct)
    {
        var result = await repository.GetByDateAsync(request.PriceDate, ct);
        if (result is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(result, cancellation: ct);
    }
}

public sealed class GetMarketPriceHistoryEndpoint(IMarketPriceRepository repository) : Endpoint<GetMarketPriceHistoryRequest, GetMarketPriceHistoryResponse>
{
    public override void Configure() { Get("/api/v1/market-prices"); Policies("ReadPolicy"); }
    public override async Task HandleAsync(GetMarketPriceHistoryRequest request, CancellationToken ct) => await Send.OkAsync(await repository.GetHistoryAsync(request, ct), cancellation: ct);
}

public sealed class DeleteMarketPriceEndpoint(IMarketPriceRepository repository) : Endpoint<DeleteMarketPriceRequest>
{
    public override void Configure() { Delete("/api/v1/market-prices/{priceDate}"); Policies("AdminPolicy"); }
    public override async Task HandleAsync(DeleteMarketPriceRequest request, CancellationToken ct)
    {
        var outcome = await repository.DeleteAtomicAsync(request.PriceDate, request.Version, request.Reason, ActorId.From(User), ct);
        if (outcome == MutationOutcome.NotFound) { await Send.NotFoundAsync(ct); return; }
        if (outcome == MutationOutcome.VersionConflict) { await Send.ResponseAsync((await repository.GetByDateAsync(request.PriceDate, ct))!, 409, cancellation: ct); return; }
        await Send.NoContentAsync(ct);
    }
}
