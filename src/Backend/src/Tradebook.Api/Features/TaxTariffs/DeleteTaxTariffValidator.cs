using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.TaxTariffs;

public sealed class DeleteTaxTariffValidator : Validator<DeleteTaxTariffRequest>
{
    public DeleteTaxTariffValidator()
    {
        RuleFor(x => x.TaxTariffId).Must(id => id.Value != Guid.Empty);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Version).GreaterThan(0);
    }
}
