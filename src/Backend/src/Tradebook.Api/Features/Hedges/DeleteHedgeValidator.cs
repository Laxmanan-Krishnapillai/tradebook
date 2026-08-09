using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.Hedges;

public sealed class DeleteHedgeValidator : Validator<DeleteHedgeRequest>
{
    public DeleteHedgeValidator()
    {
        RuleFor(x => x.HedgeId).Must(id => id.Value != Guid.Empty);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Version).GreaterThan(0);
    }
}
