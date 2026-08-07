using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreateHedgeRequest(
    Guid ContractId, DateOnly Month,
    [property: TsOptional] decimal? HedgeAmountMwh,
    [property: TsOptional] decimal? HedgePriceEurMwh);

[ExportTsInterface]
public sealed record UpdateHedgeRequest(
    Guid HedgeId,
    [property: TsOptional] decimal? HedgeAmountMwh,
    [property: TsOptional] decimal? HedgePriceEurMwh,
    long Version);

[ExportTsInterface]
public sealed record HedgeDetailsDto(
    Guid HedgeId, Guid ContractId, DateOnly Month,
    [property: TsOptional] decimal? HedgeAmountMwh,
    [property: TsOptional] decimal? HedgePriceEurMwh,
    long Version, DateTime CreatedAt, DateTime UpdatedAt);

[ExportTsInterface]
public sealed record GetHedgeHistoryRequest(
    [property: TsOptional] Guid? ContractId,
    [property: TsOptional] DateOnly? FromMonth,
    [property: TsOptional] DateOnly? ToMonth,
    int Page = 1,
    int PageSize = 50);

[ExportTsInterface]
public sealed record GetHedgeHistoryResponse(
    IReadOnlyList<HedgeDetailsDto> Items, int TotalCount, int Page, int PageSize, bool HasNextPage);

[ExportTsInterface]
public sealed record DeleteHedgeRequest(Guid HedgeId, string Reason, long Version);
