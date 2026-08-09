using System.Security.Claims;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

internal sealed class RecordingMarketPriceRepository : IMarketPriceRepository
{
    public MarketPriceDetailsDto? UpsertResult { get; set; } = HandlerGroupBTestData.MarketPrice();
    public MarketPriceDetailsDto? GetByDateResult { get; set; }
    public MutationOutcome? DeleteResult { get; set; }
    public GetMarketPriceHistoryResponse HistoryResult { get; set; } =
        new([HandlerGroupBTestData.MarketPrice()], 1, 1, 100, false);

    public List<(DateOnly PriceDate, CancellationToken Token)> GetByDateCalls { get; } = [];
    public List<(
        GetMarketPriceHistoryRequest Request,
        CancellationToken Token
    )> HistoryCalls { get; } = [];
    public List<(
        UpsertMarketPriceRequest Request,
        Guid ActorId,
        CancellationToken Token
    )> UpsertCalls { get; } = [];
    public List<(
        DateOnly PriceDate,
        long Version,
        string Reason,
        Guid ActorId,
        CancellationToken Token
    )> DeleteCalls { get; } = [];

    public Task<MarketPriceDetailsDto?> GetByDateAsync(DateOnly priceDate, CancellationToken ct)
    {
        GetByDateCalls.Add((priceDate, ct));
        return Task.FromResult(GetByDateResult);
    }

    public Task<GetMarketPriceHistoryResponse> GetHistoryAsync(
        GetMarketPriceHistoryRequest request,
        CancellationToken ct
    )
    {
        HistoryCalls.Add((request, ct));
        return Task.FromResult(HistoryResult);
    }

    public Task<MarketPriceDetailsDto?> UpsertAtomicAsync(
        UpsertMarketPriceRequest request,
        Guid actorId,
        CancellationToken ct
    )
    {
        UpsertCalls.Add((request, actorId, ct));
        return Task.FromResult(UpsertResult);
    }

    public Task<MutationOutcome?> DeleteAtomicAsync(
        DateOnly priceDate,
        long version,
        string reason,
        Guid actorId,
        CancellationToken ct
    )
    {
        DeleteCalls.Add((priceDate, version, reason, actorId, ct));
        return Task.FromResult(DeleteResult);
    }
}
