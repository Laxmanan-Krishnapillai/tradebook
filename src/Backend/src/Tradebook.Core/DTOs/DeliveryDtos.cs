using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreatePhysicalDeliveryRequest(
    Guid ContractId,
    [property: TsOptional] string? ContractInstanceId,
    string BookType,
    DateOnly SupplyMonth,
    [property: TsOptional] decimal? CapacityMw,
    [property: TsOptional] decimal? VolumeNominatedMwh,
    [property: TsOptional] decimal? VolumeRealisedMwh,
    [property: TsOptional] string? PriceMechanism,
    [property: TsOptional] DateOnly? StartDay,
    [property: TsOptional] DateOnly? EndDay);

[ExportTsInterface]
public sealed record CreatePhysicalDeliveryResponse(
    Guid DeliveryId,
    string ContractInstanceId,
    [property: TsOptional] decimal? InvoiceAmountEur,
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
    [property: TsOptional] decimal? CapacityMw,
    [property: TsOptional] decimal? VolumeNominatedMwh,
    [property: TsOptional] decimal? VolumeRealisedMwh,
    [property: TsOptional] decimal? VolumeMwh,
    [property: TsOptional] string? PriceMechanism,
    [property: TsOptional] decimal? RevenueEur,
    [property: TsOptional] decimal? SubtotalEur,
    [property: TsOptional] decimal? VatEur,
    [property: TsOptional] decimal? InvoiceAmountEur,
    string Status,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

[ExportTsInterface]
public sealed record GetDeliveryHistoryRequest(
    [property: TsOptional] Guid? ContractId,
    [property: TsOptional] string? ContractInstanceId,
    [property: TsOptional] string? BookType,
    [property: TsOptional] string? Status,
    [property: TsOptional] DateOnly? FromMonth,
    [property: TsOptional] DateOnly? ToMonth,
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
    [property: TsOptional] decimal? VolumeRealisedMwh,
    [property: TsOptional] string? Status,
    long Version);

[ExportTsInterface]
public sealed record DeletePhysicalDeliveryRequest(Guid DeliveryId, string Reason, long Version);
