using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.TaxTariffs;

public sealed class CreateTaxTariffValidator : Validator<CreateTaxTariffRequest>
{
    public CreateTaxTariffValidator()
    {
        RuleFor(x => x.ContractId).Must(id => id.Value != Guid.Empty);
        RuleFor(x => x.PeriodStart).NotEqual(default(DateOnly));
        RuleFor(x => x.PeriodEnd).GreaterThanOrEqualTo(x => x.PeriodStart);
        RuleFor(x => x.Currency).NotEmpty().Length(3).Matches("^[A-Z]{3}$");
    }
}

public sealed class UpdateTaxTariffValidator : Validator<UpdateTaxTariffRequest>
{
    public UpdateTaxTariffValidator()
    {
        RuleFor(x => x.TaxTariffId).Must(id => id.Value != Guid.Empty);
        RuleFor(x => x.Currency).NotEmpty().Length(3).Matches("^[A-Z]{3}$");
        RuleFor(x => x.Version).GreaterThan(0);
    }
}

public sealed class DeleteTaxTariffValidator : Validator<DeleteTaxTariffRequest>
{
    public DeleteTaxTariffValidator()
    {
        RuleFor(x => x.TaxTariffId).Must(id => id.Value != Guid.Empty);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Version).GreaterThan(0);
    }
}
