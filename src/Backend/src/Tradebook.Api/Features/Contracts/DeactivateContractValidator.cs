using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.Contracts;

public sealed class DeactivateContractValidator : Validator<DeactivateContractRequest>
{
    public DeactivateContractValidator()
    {
        RuleFor(x => x.ContractId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Version).GreaterThan(0);
    }
}
