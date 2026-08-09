using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Money;

namespace Tradebook.Core.DTOs;

public sealed record UpsertMarketPriceRequest
{
    public UpsertMarketPriceRequest() { }

    [SetsRequiredMembers]
    public UpsertMarketPriceRequest(
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

    public Price? TtfEurMwh { get; init; }

    public Price? EgsiEtfEurMwh { get; init; }

    public Price? TheEurMwh { get; init; }

    public Price? BgoEurMwh { get; init; }

    public Price? PgoEurMwh { get; init; }

    public Price? EuaEurMwh { get; init; }

    public Price? WithinDayMktEurMwh { get; init; }

    public Amount? EurSek { get; init; }

    public Amount? EurChf { get; init; }

    public Amount? EurGbp { get; init; }

    public Amount? EurUsd { get; init; }

    public Amount? EurDkk { get; init; }

    public long Version { get; init; }
}
