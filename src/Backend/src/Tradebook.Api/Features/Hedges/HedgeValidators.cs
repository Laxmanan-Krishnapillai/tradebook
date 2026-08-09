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

public sealed class UpdateHedgeValidator : Validator<UpdateHedgeRequest>
{
    public UpdateHedgeValidator()
    {
        RuleFor(x => x.HedgeId).Must(id => id.Value != Guid.Empty);
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

public sealed class DeleteHedgeValidator : Validator<DeleteHedgeRequest>
{
    public DeleteHedgeValidator()
    {
        RuleFor(x => x.HedgeId).Must(id => id.Value != Guid.Empty);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Version).GreaterThan(0);
    }
}
