using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.Hedges;

public sealed class CreateHedgeValidator : Validator<CreateHedgeRequest>
{
    public CreateHedgeValidator()
    {
        RuleFor(x => x.ContractId).Must(id => id.Value != Guid.Empty);
        RuleFor(x => x.Month).Must(x => x != default && x.Day == 1);
        RuleFor(x => x.HedgeAmountMwh).GreaterThanOrEqualTo(0).When(x => x.HedgeAmountMwh.HasValue);
        RuleFor(x => x.HedgePriceEurMwh)
            .GreaterThanOrEqualTo(0)
            .When(x => x.HedgePriceEurMwh.HasValue);
    }
}
