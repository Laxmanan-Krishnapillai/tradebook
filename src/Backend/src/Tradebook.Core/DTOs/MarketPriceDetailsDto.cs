using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Money;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record MarketPriceDetailsDto
{
    public MarketPriceDetailsDto() { }

    [SetsRequiredMembers]
    public MarketPriceDetailsDto(
        DateOnly PriceDate,
        Price? TtfEurMwh,
        Price? EgsiEtfEurMwh,
        Price? TheEurMwh,
        Price? BgoEurMwh,
        Price? PgoEurMwh,
        Price? EuaEurMwh,
        Price? WithinDayMktEurMwh,
        Amount? EurSek,
        Amount? EurChf,
        Amount? EurGbp,
        Amount? EurUsd,
        Amount? EurDkk,
        long Version,
        DateTime CreatedAt
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
        this.CreatedAt = CreatedAt;
    }

    public required DateOnly PriceDate { get; init; }

    [TsOptional]
    public Price? TtfEurMwh { get; init; }

    [TsOptional]
    public Price? EgsiEtfEurMwh { get; init; }

    [TsOptional]
    public Price? TheEurMwh { get; init; }

    [TsOptional]
    public Price? BgoEurMwh { get; init; }

    [TsOptional]
    public Price? PgoEurMwh { get; init; }

    [TsOptional]
    public Price? EuaEurMwh { get; init; }

    [TsOptional]
    public Price? WithinDayMktEurMwh { get; init; }

    [TsOptional]
    public Amount? EurSek { get; init; }

    [TsOptional]
    public Amount? EurChf { get; init; }

    [TsOptional]
    public Amount? EurGbp { get; init; }

    [TsOptional]
    public Amount? EurUsd { get; init; }

    [TsOptional]
    public Amount? EurDkk { get; init; }

    public required long Version { get; init; }

    public required DateTime CreatedAt { get; init; }
}
