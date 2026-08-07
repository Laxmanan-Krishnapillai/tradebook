using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreateCapacityBookingRequest(
    Guid ContractId,
    DateOnly SupplyMonth,
    [property: TsOptional] string? ContractInstanceId,
    [property: TsOptional] Guid? CounterpartyId,
    [property: TsOptional] string? BalancingGroup,
    [property: TsOptional] string? PriceMechanism,
    [property: TsOptional] string? StartArea,
    [property: TsOptional] string? EndArea,
    [property: TsOptional] string? ShipFix,
    [property: TsOptional] string? BorderPoint,
    [property: TsOptional] DateOnly? StartDay,
    [property: TsOptional] DateOnly? EndDay,
    [property: TsOptional] decimal? CapacityMw,
    [property: TsOptional] decimal? CapacityPriceEurMwh,
    [property: TsOptional] decimal? CapacityCostEur,
    [property: TsOptional] string? Comments);

[ExportTsInterface]
public sealed record UpdateCapacityBookingRequest(
    Guid CapacityBookingId,
    [property: TsOptional] string? BalancingGroup,
    [property: TsOptional] string? PriceMechanism,
    [property: TsOptional] string? StartArea,
    [property: TsOptional] string? EndArea,
    [property: TsOptional] DateOnly? StartDay,
    [property: TsOptional] DateOnly? EndDay,
    [property: TsOptional] decimal? CapacityMw,
    [property: TsOptional] decimal? CapacityPriceEurMwh,
    [property: TsOptional] decimal? CapacityCostEur,
    [property: TsOptional] string? Comments,
    long Version);

[ExportTsInterface]
public sealed record CapacityBookingDetailsDto(
    Guid CapacityBookingId, Guid ContractId, string ContractInstanceId, DateOnly SupplyMonth,
    [property: TsOptional] Guid? CounterpartyId,
    [property: TsOptional] string? BalancingGroup,
    [property: TsOptional] string? PriceMechanism,
    [property: TsOptional] string? StartArea,
    [property: TsOptional] string? EndArea,
    [property: TsOptional] string? ShipFix,
    [property: TsOptional] string? BorderPoint,
    [property: TsOptional] DateOnly? StartDay,
    [property: TsOptional] DateOnly? EndDay,
    [property: TsOptional] decimal? CapacityMw,
    [property: TsOptional] decimal? CapacityPriceEurMwh,
    [property: TsOptional] decimal? CapacityCostEur,
    [property: TsOptional] string? Comments,
    long Version, DateTime CreatedAt, DateTime UpdatedAt);

[ExportTsInterface]
public sealed record GetCapacityBookingHistoryRequest(
    [property: TsOptional] Guid? ContractId,
    [property: TsOptional] DateOnly? FromMonth,
    [property: TsOptional] DateOnly? ToMonth,
    int Page = 1,
    int PageSize = 50);

[ExportTsInterface]
public sealed record GetCapacityBookingHistoryResponse(
    IReadOnlyList<CapacityBookingDetailsDto> Items, int TotalCount, int Page, int PageSize, bool HasNextPage);

[ExportTsInterface]
public sealed record DeleteCapacityBookingRequest(Guid CapacityBookingId, string Reason, long Version);
