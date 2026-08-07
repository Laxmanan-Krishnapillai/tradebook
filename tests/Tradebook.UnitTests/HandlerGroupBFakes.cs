using System.Security.Claims;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

internal static class HandlerGroupBTestData
{
    private static readonly DateTime CreatedAt = new(2026, 2, 3, 10, 15, 0, DateTimeKind.Utc);
    private static readonly DateTime UpdatedAt = new(2026, 2, 4, 11, 30, 0, DateTimeKind.Utc);

    public static ClaimsPrincipal Principal(Guid actorId) =>
        new(new ClaimsIdentity([new Claim("sub", actorId.ToString())], "test"));

    public static HedgeDetailsDto Hedge(Guid? id = null, long version = 3) => new(
        id ?? Guid.NewGuid(),
        Guid.NewGuid(),
        new DateOnly(2026, 2, 1),
        125m,
        31.75m,
        version,
        CreatedAt,
        UpdatedAt);

    public static MarketPriceDetailsDto MarketPrice(DateOnly? priceDate = null, long version = 3) => new(
        priceDate ?? new DateOnly(2026, 2, 3),
        31.75m,
        31.50m,
        31.25m,
        31.00m,
        30.75m,
        72.50m,
        32.00m,
        11.20m,
        0.95m,
        0.84m,
        1.08m,
        7.46m,
        version,
        CreatedAt);

    public static TaxTariffDetailsDto TaxTariff(Guid? id = null, long version = 3) => new(
        id ?? Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        new DateOnly(2026, 1, 1),
        new DateOnly(2026, 12, 31),
        1.1m,
        2.2m,
        3.3m,
        4.4m,
        5.5m,
        6.6m,
        "DKK",
        version,
        CreatedAt,
        UpdatedAt);

    public static TransferDetailsDto Transfer(Guid? id = null, long version = 3) => new(
        id ?? Guid.NewGuid(),
        Guid.NewGuid(),
        "NRGD.49.GAS.THE.TRF.MON-2-2026",
        new DateOnly(2026, 2, 1),
        Guid.NewGuid(),
        "GTF",
        "THE",
        10m,
        9m,
        216m,
        -2m,
        new DateOnly(2026, 2, 1),
        new DateOnly(2026, 2, 28),
        "TTF",
        0.5m,
        0.75m,
        "Awaiting",
        "fixture transfer",
        version,
        CreatedAt,
        UpdatedAt);
}

internal sealed class RecordingHedgeRepository : IHedgeRepository
{
    public HedgeDetailsDto CreateResult { get; set; } = HandlerGroupBTestData.Hedge();
    public HedgeDetailsDto? UpdateResult { get; set; }
    public HedgeDetailsDto? GetByIdResult { get; set; }
    public MutationOutcome? DeleteResult { get; set; }
    public GetHedgeHistoryResponse HistoryResult { get; set; } =
        new([HandlerGroupBTestData.Hedge()], 1, 1, 50, false);

    public List<(Guid Id, CancellationToken Token)> GetByIdCalls { get; } = [];
    public List<(GetHedgeHistoryRequest Request, CancellationToken Token)> HistoryCalls { get; } = [];
    public List<(CreateHedgeRequest Request, Guid ActorId, CancellationToken Token)> CreateCalls { get; } = [];
    public List<(UpdateHedgeRequest Request, Guid ActorId, CancellationToken Token)> UpdateCalls { get; } = [];
    public List<(Guid Id, long Version, string Reason, Guid ActorId, CancellationToken Token)> DeleteCalls { get; } = [];

    public Task<HedgeDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        GetByIdCalls.Add((id, ct));
        return Task.FromResult(GetByIdResult);
    }

    public Task<GetHedgeHistoryResponse> GetHistoryAsync(GetHedgeHistoryRequest request, CancellationToken ct)
    {
        HistoryCalls.Add((request, ct));
        return Task.FromResult(HistoryResult);
    }

    public Task<HedgeDetailsDto> CreateAtomicAsync(CreateHedgeRequest request, Guid actorId, CancellationToken ct)
    {
        CreateCalls.Add((request, actorId, ct));
        return Task.FromResult(CreateResult);
    }

    public Task<HedgeDetailsDto?> UpdateAtomicAsync(UpdateHedgeRequest request, Guid actorId, CancellationToken ct)
    {
        UpdateCalls.Add((request, actorId, ct));
        return Task.FromResult(UpdateResult);
    }

    public Task<MutationOutcome?> DeleteAtomicAsync(Guid id, long version, string reason, Guid actorId, CancellationToken ct)
    {
        DeleteCalls.Add((id, version, reason, actorId, ct));
        return Task.FromResult(DeleteResult);
    }
}

internal sealed class RecordingMarketPriceRepository : IMarketPriceRepository
{
    public MarketPriceDetailsDto? UpsertResult { get; set; } = HandlerGroupBTestData.MarketPrice();
    public MarketPriceDetailsDto? GetByDateResult { get; set; }
    public MutationOutcome? DeleteResult { get; set; }
    public GetMarketPriceHistoryResponse HistoryResult { get; set; } =
        new([HandlerGroupBTestData.MarketPrice()], 1, 1, 100, false);

    public List<(DateOnly PriceDate, CancellationToken Token)> GetByDateCalls { get; } = [];
    public List<(GetMarketPriceHistoryRequest Request, CancellationToken Token)> HistoryCalls { get; } = [];
    public List<(UpsertMarketPriceRequest Request, Guid ActorId, CancellationToken Token)> UpsertCalls { get; } = [];
    public List<(DateOnly PriceDate, long Version, string Reason, Guid ActorId, CancellationToken Token)> DeleteCalls { get; } = [];

    public Task<MarketPriceDetailsDto?> GetByDateAsync(DateOnly priceDate, CancellationToken ct)
    {
        GetByDateCalls.Add((priceDate, ct));
        return Task.FromResult(GetByDateResult);
    }

    public Task<GetMarketPriceHistoryResponse> GetHistoryAsync(GetMarketPriceHistoryRequest request, CancellationToken ct)
    {
        HistoryCalls.Add((request, ct));
        return Task.FromResult(HistoryResult);
    }

    public Task<MarketPriceDetailsDto?> UpsertAtomicAsync(UpsertMarketPriceRequest request, Guid actorId, CancellationToken ct)
    {
        UpsertCalls.Add((request, actorId, ct));
        return Task.FromResult(UpsertResult);
    }

    public Task<MutationOutcome?> DeleteAtomicAsync(DateOnly priceDate, long version, string reason, Guid actorId, CancellationToken ct)
    {
        DeleteCalls.Add((priceDate, version, reason, actorId, ct));
        return Task.FromResult(DeleteResult);
    }
}

internal sealed class RecordingTaxTariffRepository : ITaxTariffRepository
{
    public TaxTariffDetailsDto CreateResult { get; set; } = HandlerGroupBTestData.TaxTariff();
    public TaxTariffDetailsDto? UpdateResult { get; set; }
    public TaxTariffDetailsDto? GetByIdResult { get; set; }
    public MutationOutcome? DeleteResult { get; set; }
    public GetTaxTariffHistoryResponse HistoryResult { get; set; } =
        new([HandlerGroupBTestData.TaxTariff()], 1, 1, 50, false);

    public List<(Guid Id, CancellationToken Token)> GetByIdCalls { get; } = [];
    public List<(GetTaxTariffHistoryRequest Request, CancellationToken Token)> HistoryCalls { get; } = [];
    public List<(CreateTaxTariffRequest Request, Guid ActorId, CancellationToken Token)> CreateCalls { get; } = [];
    public List<(UpdateTaxTariffRequest Request, Guid ActorId, CancellationToken Token)> UpdateCalls { get; } = [];
    public List<(Guid Id, long Version, string Reason, Guid ActorId, CancellationToken Token)> DeleteCalls { get; } = [];

    public Task<TaxTariffDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        GetByIdCalls.Add((id, ct));
        return Task.FromResult(GetByIdResult);
    }

    public Task<GetTaxTariffHistoryResponse> GetHistoryAsync(GetTaxTariffHistoryRequest request, CancellationToken ct)
    {
        HistoryCalls.Add((request, ct));
        return Task.FromResult(HistoryResult);
    }

    public Task<TaxTariffDetailsDto> CreateAtomicAsync(CreateTaxTariffRequest request, Guid actorId, CancellationToken ct)
    {
        CreateCalls.Add((request, actorId, ct));
        return Task.FromResult(CreateResult);
    }

    public Task<TaxTariffDetailsDto?> UpdateAtomicAsync(UpdateTaxTariffRequest request, Guid actorId, CancellationToken ct)
    {
        UpdateCalls.Add((request, actorId, ct));
        return Task.FromResult(UpdateResult);
    }

    public Task<MutationOutcome?> DeleteAtomicAsync(Guid id, long version, string reason, Guid actorId, CancellationToken ct)
    {
        DeleteCalls.Add((id, version, reason, actorId, ct));
        return Task.FromResult(DeleteResult);
    }
}

internal sealed class RecordingTransferRepository : ITransferRepository
{
    public TransferDetailsDto CreateResult { get; set; } = HandlerGroupBTestData.Transfer();
    public TransferDetailsDto? UpdateResult { get; set; }
    public TransferDetailsDto? GetByIdResult { get; set; }
    public MutationOutcome? CancelResult { get; set; }
    public GetTransferHistoryResponse HistoryResult { get; set; } =
        new([HandlerGroupBTestData.Transfer()], 1, 1, 50, false);

    public List<(Guid Id, CancellationToken Token)> GetByIdCalls { get; } = [];
    public List<(GetTransferHistoryRequest Request, CancellationToken Token)> HistoryCalls { get; } = [];
    public List<(CreateTransferRequest Request, Guid ActorId, CancellationToken Token)> CreateCalls { get; } = [];
    public List<(UpdateTransferRequest Request, Guid ActorId, CancellationToken Token)> UpdateCalls { get; } = [];
    public List<(Guid Id, long Version, string Reason, Guid ActorId, CancellationToken Token)> CancelCalls { get; } = [];

    public Task<TransferDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        GetByIdCalls.Add((id, ct));
        return Task.FromResult(GetByIdResult);
    }

    public Task<GetTransferHistoryResponse> GetHistoryAsync(GetTransferHistoryRequest request, CancellationToken ct)
    {
        HistoryCalls.Add((request, ct));
        return Task.FromResult(HistoryResult);
    }

    public Task<TransferDetailsDto> CreateAtomicAsync(CreateTransferRequest request, Guid actorId, CancellationToken ct)
    {
        CreateCalls.Add((request, actorId, ct));
        return Task.FromResult(CreateResult);
    }

    public Task<TransferDetailsDto?> UpdateAtomicAsync(UpdateTransferRequest request, Guid actorId, CancellationToken ct)
    {
        UpdateCalls.Add((request, actorId, ct));
        return Task.FromResult(UpdateResult);
    }

    public Task<MutationOutcome?> CancelAtomicAsync(Guid id, long version, string reason, Guid actorId, CancellationToken ct)
    {
        CancelCalls.Add((id, version, reason, actorId, ct));
        return Task.FromResult(CancelResult);
    }
}
