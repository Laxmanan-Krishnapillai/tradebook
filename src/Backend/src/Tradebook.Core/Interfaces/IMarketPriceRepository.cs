using Tradebook.Core.DTOs;

namespace Tradebook.Core.Interfaces;

public interface IMarketPriceRepository
{
    Task<MarketPriceDetailsDto?> GetByDateAsync(DateOnly priceDate, CancellationToken ct);
    Task<GetMarketPriceHistoryResponse> GetHistoryAsync(GetMarketPriceHistoryRequest request, CancellationToken ct);
    Task<MarketPriceDetailsDto?> UpsertAtomicAsync(UpsertMarketPriceRequest request, Guid actorId, CancellationToken ct);
    Task<MutationOutcome?> DeleteAtomicAsync(DateOnly priceDate, long version, string reason, Guid actorId, CancellationToken ct);
}
