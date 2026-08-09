using Tradebook.Core.Domain.ValueObjects.Money;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record UpsertMarketPriceRequest(
    DateOnly PriceDate,
    [property: TsOptional] Price? TtfEurMwh,
    [property: TsOptional] Price? EgsiEtfEurMwh,
    [property: TsOptional] Price? TheEurMwh,
    [property: TsOptional] Price? BgoEurMwh,
    [property: TsOptional] Price? PgoEurMwh,
    [property: TsOptional] Price? EuaEurMwh,
    [property: TsOptional] Price? WithinDayMktEurMwh,
    [property: TsOptional] Amount? EurSek,
    [property: TsOptional] Amount? EurChf,
    [property: TsOptional] Amount? EurGbp,
    [property: TsOptional] Amount? EurUsd,
    [property: TsOptional] Amount? EurDkk,
    long Version = 0
);

[ExportTsInterface]
public sealed record MarketPriceDetailsDto(
    DateOnly PriceDate,
    [property: TsOptional] Price? TtfEurMwh,
    [property: TsOptional] Price? EgsiEtfEurMwh,
    [property: TsOptional] Price? TheEurMwh,
    [property: TsOptional] Price? BgoEurMwh,
    [property: TsOptional] Price? PgoEurMwh,
    [property: TsOptional] Price? EuaEurMwh,
    [property: TsOptional] Price? WithinDayMktEurMwh,
    [property: TsOptional] Amount? EurSek,
    [property: TsOptional] Amount? EurChf,
    [property: TsOptional] Amount? EurGbp,
    [property: TsOptional] Amount? EurUsd,
    [property: TsOptional] Amount? EurDkk,
    long Version,
    DateTime CreatedAt
);

[ExportTsInterface]
public sealed record GetMarketPriceHistoryRequest(
    [property: TsOptional] DateOnly? FromDate,
    [property: TsOptional] DateOnly? ToDate,
    int Page = 1,
    int PageSize = 100
);

[ExportTsInterface]
public sealed record GetMarketPriceHistoryResponse(
    IReadOnlyList<MarketPriceDetailsDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    bool HasNextPage
);

[ExportTsInterface]
public sealed record DeleteMarketPriceRequest(DateOnly PriceDate, string Reason, long Version);
