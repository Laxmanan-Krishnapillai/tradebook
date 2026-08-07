using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record UpsertMarketPriceRequest(
    DateOnly PriceDate,
    [property: TsOptional] decimal? TtfEurMwh,
    [property: TsOptional] decimal? EgsiEtfEurMwh,
    [property: TsOptional] decimal? TheEurMwh,
    [property: TsOptional] decimal? BgoEurMwh,
    [property: TsOptional] decimal? PgoEurMwh,
    [property: TsOptional] decimal? EuaEurMwh,
    [property: TsOptional] decimal? WithinDayMktEurMwh,
    [property: TsOptional] decimal? EurSek,
    [property: TsOptional] decimal? EurChf,
    [property: TsOptional] decimal? EurGbp,
    [property: TsOptional] decimal? EurUsd,
    [property: TsOptional] decimal? EurDkk,
    long Version = 0);

[ExportTsInterface]
public sealed record MarketPriceDetailsDto(
    DateOnly PriceDate,
    [property: TsOptional] decimal? TtfEurMwh,
    [property: TsOptional] decimal? EgsiEtfEurMwh,
    [property: TsOptional] decimal? TheEurMwh,
    [property: TsOptional] decimal? BgoEurMwh,
    [property: TsOptional] decimal? PgoEurMwh,
    [property: TsOptional] decimal? EuaEurMwh,
    [property: TsOptional] decimal? WithinDayMktEurMwh,
    [property: TsOptional] decimal? EurSek,
    [property: TsOptional] decimal? EurChf,
    [property: TsOptional] decimal? EurGbp,
    [property: TsOptional] decimal? EurUsd,
    [property: TsOptional] decimal? EurDkk,
    long Version, DateTime CreatedAt);

[ExportTsInterface]
public sealed record GetMarketPriceHistoryRequest(
    [property: TsOptional] DateOnly? FromDate,
    [property: TsOptional] DateOnly? ToDate,
    int Page = 1,
    int PageSize = 100);

[ExportTsInterface]
public sealed record GetMarketPriceHistoryResponse(
    IReadOnlyList<MarketPriceDetailsDto> Items, int TotalCount, int Page, int PageSize, bool HasNextPage);

[ExportTsInterface]
public sealed record DeleteMarketPriceRequest(DateOnly PriceDate, string Reason, long Version);
