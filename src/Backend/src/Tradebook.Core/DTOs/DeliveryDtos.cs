using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreatePhysicalDeliveryRequest(
    ContractId ContractId,
    [property: TsOptional] string? ContractInstanceId,
    string BookType,
    DateOnly SupplyMonth,
    [property: TsOptional] Quantity? CapacityMw,
    [property: TsOptional] Quantity? VolumeNominatedMwh,
    [property: TsOptional] Quantity? VolumeRealisedMwh,
    [property: TsOptional] string? PriceMechanism,
    [property: TsOptional] DateOnly? StartDay,
    [property: TsOptional] DateOnly? EndDay
);

[ExportTsInterface]
public sealed record CreatePhysicalDeliveryResponse(
    DeliveryId DeliveryId,
    string ContractInstanceId,
    [property: TsOptional] Amount? InvoiceAmountEur,
    string Status,
    long Version,
    DateTimeOffset CreatedAt
);

[ExportTsInterface]
public sealed record PhysicalDeliveryDetailsDto(
    DeliveryId DeliveryId,
    ContractId ContractId,
    string ContractInstanceId,
    string BookType,
    DateOnly SupplyMonth,
    [property: TsOptional] Quantity? CapacityMw,
    [property: TsOptional] Quantity? VolumeNominatedMwh,
    [property: TsOptional] Quantity? VolumeRealisedMwh,
    [property: TsOptional] Quantity? VolumeMwh,
    [property: TsOptional] string? PriceMechanism,
    [property: TsOptional] Amount? RevenueEur,
    [property: TsOptional] Amount? SubtotalEur,
    [property: TsOptional] Amount? VatEur,
    [property: TsOptional] Amount? InvoiceAmountEur,
    string Status,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

[ExportTsInterface]
public sealed record GetDeliveryHistoryRequest(
    [property: TsOptional] ContractId? ContractId,
    [property: TsOptional] string? ContractInstanceId,
    [property: TsOptional] string? BookType,
    [property: TsOptional] string? Status,
    [property: TsOptional] DateOnly? FromMonth,
    [property: TsOptional] DateOnly? ToMonth,
    int Page = 1,
    int PageSize = 50
);

[ExportTsInterface]
public sealed record GetDeliveryHistoryResponse(
    IReadOnlyList<PhysicalDeliveryDetailsDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    bool HasNextPage
);

[ExportTsInterface]
public sealed record UpdatePhysicalDeliveryRequest(
    DeliveryId DeliveryId,
    [property: TsOptional] Quantity? VolumeRealisedMwh,
    [property: TsOptional] string? Status,
    long Version
);

[ExportTsInterface]
public sealed record DeletePhysicalDeliveryRequest(
    DeliveryId DeliveryId,
    string Reason,
    long Version
);
