using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.MarketPrices;

public sealed class UpsertMarketPriceValidator : Validator<UpsertMarketPriceRequest>
{
    public UpsertMarketPriceValidator()
    {
        RuleFor(x => x.PriceDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Version).GreaterThanOrEqualTo(0);
        RuleFor(x => x)
            .Must(x =>
                x.TtfEurMwh.HasValue
                || x.EgsiEtfEurMwh.HasValue
                || x.TheEurMwh.HasValue
                || x.BgoEurMwh.HasValue
                || x.PgoEurMwh.HasValue
                || x.EuaEurMwh.HasValue
                || x.WithinDayMktEurMwh.HasValue
                || x.EurSek.HasValue
                || x.EurChf.HasValue
                || x.EurGbp.HasValue
                || x.EurUsd.HasValue
                || x.EurDkk.HasValue
            )
            .WithMessage("At least one market or FX value is required.");
        RuleFor(x => x.EurSek).GreaterThan(0).When(x => x.EurSek.HasValue);
        RuleFor(x => x.EurChf).GreaterThan(0).When(x => x.EurChf.HasValue);
        RuleFor(x => x.EurGbp).GreaterThan(0).When(x => x.EurGbp.HasValue);
        RuleFor(x => x.EurUsd).GreaterThan(0).When(x => x.EurUsd.HasValue);
        RuleFor(x => x.EurDkk).GreaterThan(0).When(x => x.EurDkk.HasValue);
    }
}
