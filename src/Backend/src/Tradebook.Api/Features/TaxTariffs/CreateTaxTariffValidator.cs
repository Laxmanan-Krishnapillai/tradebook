using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.TaxTariffs;

public sealed class CreateTaxTariffValidator : Validator<CreateTaxTariffRequest>
{
    public CreateTaxTariffValidator()
    {
        RuleFor(x => x.ContractId).NotEmpty();
        RuleFor(x => x.PeriodStart).NotEqual(default(DateOnly));
        RuleFor(x => x.PeriodEnd).GreaterThanOrEqualTo(x => x.PeriodStart);
        RuleFor(x => x.Currency).NotEmpty().Length(3).Matches("^[A-Z]{3}$");
    }
}
