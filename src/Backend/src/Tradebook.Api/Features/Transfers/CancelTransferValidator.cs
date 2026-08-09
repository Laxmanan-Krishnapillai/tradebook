using FastEndpoints;
using FluentValidation;
using Tradebook.Api.Validation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.Transfers;

public sealed class CancelTransferValidator : Validator<CancelTransferRequest>
{
    public CancelTransferValidator()
    {
        RuleFor(x => x.TransferId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Version).GreaterThan(0);
    }
}
