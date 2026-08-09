using System.Security.Claims;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

internal static class DomainEndpointTestData
{
    private static readonly DateTime CreatedAt = new(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime UpdatedAt = new(2026, 1, 3, 11, 30, 0, DateTimeKind.Utc);

    public static ClaimsPrincipal Principal(Guid actorId) =>
        new(
            new ClaimsIdentity(
                [
                    new Claim("oid", actorId.ToString()),
                    new("tid", "11111111-1111-1111-1111-111111111111"),
                    new("tradebook_tenant", "11111111-1111-1111-1111-111111111111"),
                ],
                "test"
            )
        );

    public static BioticketDetailsDto Bioticket(
        Guid? id = null,
        long version = 4,
        string status = "Awaiting"
    ) =>
        new(
            id ?? Guid.NewGuid(),
            Guid.NewGuid(),
            "CRSB45.ST.2401.CO2E-1-2026",
            "Sales",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31),
            12m,
            11m,
            10m,
            20m,
            200m,
            0.25m,
            50m,
            250m,
            status,
            "fixture bioticket",
            version,
            CreatedAt,
            UpdatedAt
        );

    public static CapacityBookingDetailsDto CapacityBooking(Guid? id = null, long version = 4) =>
        new(
            id ?? Guid.NewGuid(),
            Guid.NewGuid(),
            "NRGD.49.GAS.THE.CBC.MON-1-2026",
            new DateOnly(2026, 1, 1),
            Guid.NewGuid(),
            "NRGD",
            "GTF/THE - Monthly",
            "GTF",
            "THE",
            "GTF-ELLUND-THE",
            "ELLUND",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31),
            10m,
            2m,
            20m,
            "fixture booking",
            version,
            CreatedAt,
            UpdatedAt
        );

    public static ContractDetailsDto Contract(
        Guid? id = null,
        long version = 4,
        bool isActive = true
    ) =>
        new(
            id ?? Guid.NewGuid(),
            "ARLA45.SC.2601.ETSS",
            Guid.NewGuid(),
            "GoO",
            "Sell",
            "ARLA",
            "DK",
            45,
            1,
            2026,
            null,
            Guid.NewGuid(),
            "BGEM",
            "ETS",
            "SUB",
            "TTF",
            31.25m,
            "External",
            "fixture contract",
            isActive,
            version,
            CreatedAt,
            UpdatedAt
        );

    public static GooCertificateTransactionDetailsDto GooCertificate(
        Guid? id = null,
        long version = 4,
        string status = "Processing"
    ) =>
        new(
            id ?? Guid.NewGuid(),
            "a07TG00000PMLtSYAX",
            "7265-17552",
            "Dena-Internal transaction",
            "847513",
            "NL",
            Guid.NewGuid(),
            "Producer AS",
            2.75m,
            new DateOnly(2026, 1, 1),
            null,
            null,
            "Dena",
            status,
            new DateOnly(2026, 1, 2),
            100m,
            100m,
            "Biogas",
            "fixture certificate",
            version,
            CreatedAt,
            UpdatedAt
        );
}
