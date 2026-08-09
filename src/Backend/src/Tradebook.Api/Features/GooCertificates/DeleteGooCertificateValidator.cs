using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.GooCertificates;

public sealed class DeleteGooCertificateValidator
    : Validator<DeleteGooCertificateTransactionRequest>
{
    public DeleteGooCertificateValidator()
    {
        RuleFor(x => x.GooCertificateTransactionId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Version).GreaterThan(0);
    }
}
