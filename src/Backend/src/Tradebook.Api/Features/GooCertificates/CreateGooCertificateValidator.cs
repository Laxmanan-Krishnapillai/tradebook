using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.GooCertificates;

public sealed class CreateGooCertificateValidator
    : Validator<CreateGooCertificateTransactionRequest>
{
    public CreateGooCertificateValidator()
    {
        RuleFor(x => x.Status).Must(GooValidation.Status);
        RuleFor(x => x.CountryOfProduction).Length(2).When(x => x.CountryOfProduction is not null);
        RuleFor(x => x)
            .Must(x => x.ProducerContractId.HasValue || x.CustomerContractId.HasValue)
            .WithMessage("At least one producer or customer contract is required.");
    }
}
