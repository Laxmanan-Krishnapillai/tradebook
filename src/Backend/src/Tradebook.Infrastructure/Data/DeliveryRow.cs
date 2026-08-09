namespace Tradebook.Infrastructure.Data;

internal sealed record DeliveryRow(
    Guid DeliveryId,
    Guid ContractId,
    string ContractInstanceId,
    string BookType,
    DateOnly SupplyMonth,
    decimal? CapacityMw,
    decimal? VolumeNominatedMwh,
    decimal? VolumeRealisedMwh,
    decimal? VolumeMwh,
    string? PriceMechanism,
    decimal? RevenueEur,
    decimal? SubtotalEur,
    decimal? VatEur,
    decimal? InvoiceAmountEur,
    string Status,
    long Version,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
