using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreateTransferRequest(
    Guid ContractId,
    DateOnly SupplyMonth,
    [property: TsOptional] string? ContractInstanceId,
    [property: TsOptional] Guid? CounterpartyId,
    [property: TsOptional] string? BalancingGroup,
    [property: TsOptional] string? TradingArea,
    [property: TsOptional] decimal? CapacityMw,
    [property: TsOptional] decimal? BookedCapacityMw,
    [property: TsOptional] decimal? VolumeMwh,
    [property: TsOptional] decimal? BalancingEffectMwh,
    [property: TsOptional] DateOnly? StartDay,
    [property: TsOptional] DateOnly? EndDay,
    [property: TsOptional] string? PriceMechanism,
    [property: TsOptional] decimal? TransportCostEurMwh,
    [property: TsOptional] decimal? CapacityCostEurMwh,
    [property: TsOptional] string? Status,
    [property: TsOptional] string? Comments);

[ExportTsInterface]
public sealed record UpdateTransferRequest(
    Guid TransferId,
    [property: TsOptional] string? TradingArea,
    [property: TsOptional] decimal? CapacityMw,
    [property: TsOptional] decimal? BookedCapacityMw,
    [property: TsOptional] decimal? VolumeMwh,
    [property: TsOptional] decimal? BalancingEffectMwh,
    [property: TsOptional] string? PriceMechanism,
    [property: TsOptional] decimal? TransportCostEurMwh,
    [property: TsOptional] decimal? CapacityCostEurMwh,
    [property: TsOptional] string? Status,
    [property: TsOptional] string? Comments,
    long Version);

[ExportTsInterface]
public sealed record TransferDetailsDto(
    Guid TransferId, Guid ContractId, string ContractInstanceId, DateOnly SupplyMonth,
    [property: TsOptional] Guid? CounterpartyId,
    [property: TsOptional] string? BalancingGroup,
    [property: TsOptional] string? TradingArea,
    [property: TsOptional] decimal? CapacityMw,
    [property: TsOptional] decimal? BookedCapacityMw,
    [property: TsOptional] decimal? VolumeMwh,
    [property: TsOptional] decimal? BalancingEffectMwh,
    [property: TsOptional] DateOnly? StartDay,
    [property: TsOptional] DateOnly? EndDay,
    [property: TsOptional] string? PriceMechanism,
    [property: TsOptional] decimal? TransportCostEurMwh,
    [property: TsOptional] decimal? CapacityCostEurMwh,
    [property: TsOptional] string? Status,
    [property: TsOptional] string? Comments,
    long Version, DateTime CreatedAt, DateTime UpdatedAt);

[ExportTsInterface]
public sealed record GetTransferHistoryRequest(
    [property: TsOptional] Guid? ContractId,
    [property: TsOptional] string? Status,
    [property: TsOptional] DateOnly? FromMonth,
    [property: TsOptional] DateOnly? ToMonth,
    int Page = 1,
    int PageSize = 50);

[ExportTsInterface]
public sealed record GetTransferHistoryResponse(
    IReadOnlyList<TransferDetailsDto> Items, int TotalCount, int Page, int PageSize, bool HasNextPage);

[ExportTsInterface]
public sealed record CancelTransferRequest(Guid TransferId, string Reason, long Version);
