using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreateCapacityBookingRequest(
    ContractId ContractId,
    DateOnly SupplyMonth,
    [property: TsOptional] string? ContractInstanceId,
    [property: TsOptional] CounterpartyId? CounterpartyId,
    [property: TsOptional] string? BalancingGroup,
    [property: TsOptional] string? PriceMechanism,
    [property: TsOptional] string? StartArea,
    [property: TsOptional] string? EndArea,
    [property: TsOptional] string? ShipFix,
    [property: TsOptional] string? BorderPoint,
    [property: TsOptional] DateOnly? StartDay,
    [property: TsOptional] DateOnly? EndDay,
    [property: TsOptional] Quantity? CapacityMw,
    [property: TsOptional] Quantity? CapacityPriceEurMwh,
    [property: TsOptional] Quantity? CapacityCostEur,
    [property: TsOptional] string? Comments);

[ExportTsInterface]
public sealed record UpdateCapacityBookingRequest(
    CapacityBookingId CapacityBookingId,
    [property: TsOptional] string? BalancingGroup,
    [property: TsOptional] string? PriceMechanism,
    [property: TsOptional] string? StartArea,
    [property: TsOptional] string? EndArea,
    [property: TsOptional] DateOnly? StartDay,
    [property: TsOptional] DateOnly? EndDay,
    [property: TsOptional] Quantity? CapacityMw,
    [property: TsOptional] Quantity? CapacityPriceEurMwh,
    [property: TsOptional] Quantity? CapacityCostEur,
    [property: TsOptional] string? Comments,
    long Version);

[ExportTsInterface]
public sealed record CapacityBookingDetailsDto(
    CapacityBookingId CapacityBookingId, ContractId ContractId, string ContractInstanceId, DateOnly SupplyMonth,
    [property: TsOptional] CounterpartyId? CounterpartyId,
    [property: TsOptional] string? BalancingGroup,
    [property: TsOptional] string? PriceMechanism,
    [property: TsOptional] string? StartArea,
    [property: TsOptional] string? EndArea,
    [property: TsOptional] string? ShipFix,
    [property: TsOptional] string? BorderPoint,
    [property: TsOptional] DateOnly? StartDay,
    [property: TsOptional] DateOnly? EndDay,
    [property: TsOptional] Quantity? CapacityMw,
    [property: TsOptional] Quantity? CapacityPriceEurMwh,
    [property: TsOptional] Quantity? CapacityCostEur,
    [property: TsOptional] string? Comments,
    long Version, DateTime CreatedAt, DateTime UpdatedAt);

[ExportTsInterface]
public sealed record GetCapacityBookingHistoryRequest(
    [property: TsOptional] ContractId? ContractId,
    [property: TsOptional] DateOnly? FromMonth,
    [property: TsOptional] DateOnly? ToMonth,
    int Page = 1,
    int PageSize = 50);

[ExportTsInterface]
public sealed record GetCapacityBookingHistoryResponse(
    IReadOnlyList<CapacityBookingDetailsDto> Items, int TotalCount, int Page, int PageSize, bool HasNextPage);

[ExportTsInterface]
public sealed record DeleteCapacityBookingRequest(CapacityBookingId CapacityBookingId, string Reason, long Version);
