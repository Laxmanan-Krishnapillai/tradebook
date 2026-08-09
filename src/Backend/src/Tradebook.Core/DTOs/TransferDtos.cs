using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreateTransferRequest(
    ContractId ContractId,
    DateOnly SupplyMonth,
    [property: TsOptional] string? ContractInstanceId,
    [property: TsOptional] CounterpartyId? CounterpartyId,
    [property: TsOptional] string? BalancingGroup,
    [property: TsOptional] string? TradingArea,
    [property: TsOptional] Quantity? CapacityMw,
    [property: TsOptional] Quantity? BookedCapacityMw,
    [property: TsOptional] Quantity? VolumeMwh,
    [property: TsOptional] Quantity? BalancingEffectMwh,
    [property: TsOptional] DateOnly? StartDay,
    [property: TsOptional] DateOnly? EndDay,
    [property: TsOptional] string? PriceMechanism,
    [property: TsOptional] Amount? TransportCostEurMwh,
    [property: TsOptional] Quantity? CapacityCostEurMwh,
    [property: TsOptional] string? Status,
    [property: TsOptional] string? Comments
);

[ExportTsInterface]
public sealed record UpdateTransferRequest(
    TransferId TransferId,
    [property: TsOptional] string? TradingArea,
    [property: TsOptional] Quantity? CapacityMw,
    [property: TsOptional] Quantity? BookedCapacityMw,
    [property: TsOptional] Quantity? VolumeMwh,
    [property: TsOptional] Quantity? BalancingEffectMwh,
    [property: TsOptional] string? PriceMechanism,
    [property: TsOptional] Amount? TransportCostEurMwh,
    [property: TsOptional] Quantity? CapacityCostEurMwh,
    [property: TsOptional] string? Status,
    [property: TsOptional] string? Comments,
    long Version
);

[ExportTsInterface]
public sealed record TransferDetailsDto(
    TransferId TransferId,
    ContractId ContractId,
    string ContractInstanceId,
    DateOnly SupplyMonth,
    [property: TsOptional] CounterpartyId? CounterpartyId,
    [property: TsOptional] string? BalancingGroup,
    [property: TsOptional] string? TradingArea,
    [property: TsOptional] Quantity? CapacityMw,
    [property: TsOptional] Quantity? BookedCapacityMw,
    [property: TsOptional] Quantity? VolumeMwh,
    [property: TsOptional] Quantity? BalancingEffectMwh,
    [property: TsOptional] DateOnly? StartDay,
    [property: TsOptional] DateOnly? EndDay,
    [property: TsOptional] string? PriceMechanism,
    [property: TsOptional] Amount? TransportCostEurMwh,
    [property: TsOptional] Quantity? CapacityCostEurMwh,
    [property: TsOptional] string? Status,
    [property: TsOptional] string? Comments,
    long Version,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

[ExportTsInterface]
public sealed record GetTransferHistoryRequest(
    [property: TsOptional] ContractId? ContractId,
    [property: TsOptional] string? Status,
    [property: TsOptional] DateOnly? FromMonth,
    [property: TsOptional] DateOnly? ToMonth,
    int Page = 1,
    int PageSize = 50
);

[ExportTsInterface]
public sealed record GetTransferHistoryResponse(
    IReadOnlyList<TransferDetailsDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    bool HasNextPage
);

[ExportTsInterface]
public sealed record CancelTransferRequest(TransferId TransferId, string Reason, long Version);
