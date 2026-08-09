using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.TaxTariffs;

public sealed class UpdateTaxTariffValidator : Validator<UpdateTaxTariffRequest>
{
    public UpdateTaxTariffValidator()
    {
        RuleFor(x => x.TaxTariffId).NotEmpty();
        RuleFor(x => x.Currency).NotEmpty().Length(3).Matches("^[A-Z]{3}$");
        RuleFor(x => x.Version).GreaterThan(0);
    }
}
