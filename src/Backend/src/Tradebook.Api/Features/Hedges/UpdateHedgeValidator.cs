using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.Hedges;

public sealed class UpdateHedgeValidator : Validator<UpdateHedgeRequest>
{
    public UpdateHedgeValidator()
    {
        RuleFor(x => x.HedgeId).NotEmpty();
        RuleFor(x => x.HedgeAmountMwh).GreaterThanOrEqualTo(0).When(x => x.HedgeAmountMwh.HasValue);
        RuleFor(x => x.HedgePriceEurMwh)
            .GreaterThanOrEqualTo(0)
            .When(x => x.HedgePriceEurMwh.HasValue);
        RuleFor(x => x.Version).GreaterThan(0);
        RuleFor(x => x)
            .Must(x => x.HedgeAmountMwh.HasValue || x.HedgePriceEurMwh.HasValue)
            .WithMessage("At least one mutable field is required.");
    }
}
