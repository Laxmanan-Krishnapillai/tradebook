using FastEndpoints;
using FluentValidation;
using Tradebook.Api.Validation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.Biotickets;

public sealed class CancelBioticketValidator : Validator<CancelBioticketRequest>
{
    public CancelBioticketValidator()
    {
        RuleFor(x => x.BioticketId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Version).GreaterThan(0);
    }
}
