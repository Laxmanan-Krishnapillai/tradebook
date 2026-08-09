using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreateHedgeRequest(
    ContractId ContractId,
    DateOnly Month,
    [property: TsOptional] Quantity? HedgeAmountMwh,
    [property: TsOptional] Price? HedgePriceEurMwh
);

[ExportTsInterface]
public sealed record UpdateHedgeRequest(
    HedgeId HedgeId,
    [property: TsOptional] Quantity? HedgeAmountMwh,
    [property: TsOptional] Price? HedgePriceEurMwh,
    long Version
);

[ExportTsInterface]
public sealed record HedgeDetailsDto(
    HedgeId HedgeId,
    ContractId ContractId,
    DateOnly Month,
    [property: TsOptional] Quantity? HedgeAmountMwh,
    [property: TsOptional] Price? HedgePriceEurMwh,
    long Version,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

[ExportTsInterface]
public sealed record GetHedgeHistoryRequest(
    [property: TsOptional] ContractId? ContractId,
    [property: TsOptional] DateOnly? FromMonth,
    [property: TsOptional] DateOnly? ToMonth,
    int Page = 1,
    int PageSize = 50
);

[ExportTsInterface]
public sealed record GetHedgeHistoryResponse(
    IReadOnlyList<HedgeDetailsDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    bool HasNextPage
);

[ExportTsInterface]
public sealed record DeleteHedgeRequest(HedgeId HedgeId, string Reason, long Version);
