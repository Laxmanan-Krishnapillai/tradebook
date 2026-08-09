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

    public static HedgeDetailsDto Hedge(Guid? id = null, long version = 3) =>
        new(
            id ?? Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 2, 1),
            125m,
            31.75m,
            version,
            CreatedAt,
            UpdatedAt
        );

    public static MarketPriceDetailsDto MarketPrice(DateOnly? priceDate = null, long version = 3) =>
        new(
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
            CreatedAt
        );

    public static TaxTariffDetailsDto TaxTariff(Guid? id = null, long version = 3) =>
        new(
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
            UpdatedAt
        );

    public static TransferDetailsDto Transfer(Guid? id = null, long version = 3) =>
        new(
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
            UpdatedAt
        );
}
