using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreatePhysicalDeliveryRequest(
    Guid ContractId,
    string ContractInstanceId,
    string BookType,
    DateOnly SupplyMonth,
    decimal? CapacityMw,
    decimal? VolumeNominatedMwh,
    decimal? VolumeRealisedMwh,
    string? PriceMechanism,
    DateOnly? StartDay,
    DateOnly? EndDay);

[ExportTsInterface]
public sealed record CreatePhysicalDeliveryResponse(
    Guid DeliveryId,
    string ContractInstanceId,
    decimal? InvoiceAmountEur,
    string Status,
    long Version,
    DateTimeOffset CreatedAt);

[ExportTsInterface]
public sealed record PhysicalDeliveryDetailsDto(
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
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

[ExportTsInterface]
public sealed record GetDeliveryHistoryRequest(
    Guid? ContractId,
    string? ContractInstanceId,
    string? BookType,
    string? Status,
    DateOnly? FromMonth,
    DateOnly? ToMonth,
    int Page = 1,
    int PageSize = 50);

[ExportTsInterface]
public sealed record GetDeliveryHistoryResponse(
    IReadOnlyList<PhysicalDeliveryDetailsDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    bool HasNextPage);

[ExportTsInterface]
public sealed record UpdatePhysicalDeliveryRequest(
    Guid DeliveryId,
    decimal? VolumeRealisedMwh,
    string? Status,
    long Version);

[ExportTsInterface]
public sealed record DeletePhysicalDeliveryRequest(Guid DeliveryId, string Reason, long Version);
