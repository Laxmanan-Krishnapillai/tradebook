using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record UpsertMarketPriceRequest
{
    public UpsertMarketPriceRequest() { }

    [SetsRequiredMembers]
    public UpsertMarketPriceRequest(
        DateOnly PriceDate,
        decimal? TtfEurMwh,
        decimal? EgsiEtfEurMwh,
        decimal? TheEurMwh,
        decimal? BgoEurMwh,
        decimal? PgoEurMwh,
        decimal? EuaEurMwh,
        decimal? WithinDayMktEurMwh,
        decimal? EurSek,
        decimal? EurChf,
        decimal? EurGbp,
        decimal? EurUsd,
        decimal? EurDkk,
        long Version = 0
    )
    {
        this.PriceDate = PriceDate;
        this.TtfEurMwh = TtfEurMwh;
        this.EgsiEtfEurMwh = EgsiEtfEurMwh;
        this.TheEurMwh = TheEurMwh;
        this.BgoEurMwh = BgoEurMwh;
        this.PgoEurMwh = PgoEurMwh;
        this.EuaEurMwh = EuaEurMwh;
        this.WithinDayMktEurMwh = WithinDayMktEurMwh;
        this.EurSek = EurSek;
        this.EurChf = EurChf;
        this.EurGbp = EurGbp;
        this.EurUsd = EurUsd;
        this.EurDkk = EurDkk;
        this.Version = Version;
    }

    public required DateOnly PriceDate { get; init; }

    [TsOptional]
    public decimal? TtfEurMwh { get; init; }

    [TsOptional]
    public decimal? EgsiEtfEurMwh { get; init; }

    [TsOptional]
    public decimal? TheEurMwh { get; init; }

    [TsOptional]
    public decimal? BgoEurMwh { get; init; }

    [TsOptional]
    public decimal? PgoEurMwh { get; init; }

    [TsOptional]
    public decimal? EuaEurMwh { get; init; }

    [TsOptional]
    public decimal? WithinDayMktEurMwh { get; init; }

    [TsOptional]
    public decimal? EurSek { get; init; }

    [TsOptional]
    public decimal? EurChf { get; init; }

    [TsOptional]
    public decimal? EurGbp { get; init; }

    [TsOptional]
    public decimal? EurUsd { get; init; }

    [TsOptional]
    public decimal? EurDkk { get; init; }

    public long Version { get; init; }
}
