using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.GooCertificates;

public sealed class DeleteGooCertificateValidator
    : Validator<DeleteGooCertificateTransactionRequest>
{
    public DeleteGooCertificateValidator()
    {
        RuleFor(x => x.GooCertificateTransactionId).Must(id => id.Value != Guid.Empty);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Version).GreaterThan(0);
    }
}
