using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.Contracts;

public sealed class CreateContractValidator : Validator<CreateContractRequest>
{
    public CreateContractValidator()
    {
        RuleFor(x => x.ContractName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CounterpartyId).NotEmpty();
        RuleFor(x => x.ProductType).Must(x => x is "GoO" or "Gas" or "GoO+Gas" or "GoO+Gas+Shipping" or "Tickets");
        RuleFor(x => x.Action).Must(x => x is "Buy" or "Sell" or "Intercompany" or "Swap");
        RuleFor(x => x.GooQuality).Must(x => x is null or "RED" or "ETS" or "OZD" or "NMS" or "EWG" or "ISCC" or "NOQ" or "GEG" or "RTFO" or "BHG");
        RuleFor(x => x.SubsidyStatus).Must(x => x is null or "SUB" or "UNS" or "None");
        RuleFor(x => x.PriceMechanismGas).Must(x => x is null or "FIXED" or "VARIABLE" or "EGSI ETF" or "TTF" or "WITHIN-DAY MKT" or "BGO" or "PGO" or "THE");
        RuleFor(x => x.ContractType).Must(x => x is null or "External" or "Intercompany");
    }
}

public sealed class UpdateContractValidator : Validator<UpdateContractRequest>
{
    public UpdateContractValidator()
    {
        Include(new CreateContractValidatorAdapter());
        RuleFor(x => x.ContractId).NotEmpty();
        RuleFor(x => x.Version).GreaterThan(0);
    }

    private sealed class CreateContractValidatorAdapter : AbstractValidator<UpdateContractRequest>
    {
        public CreateContractValidatorAdapter()
        {
            RuleFor(x => x.ContractName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.CounterpartyId).NotEmpty();
            RuleFor(x => x.ProductType).Must(x => x is "GoO" or "Gas" or "GoO+Gas" or "GoO+Gas+Shipping" or "Tickets");
            RuleFor(x => x.Action).Must(x => x is "Buy" or "Sell" or "Intercompany" or "Swap");
            RuleFor(x => x.GooQuality).Must(x => x is null or "RED" or "ETS" or "OZD" or "NMS" or "EWG" or "ISCC" or "NOQ" or "GEG" or "RTFO" or "BHG");
            RuleFor(x => x.SubsidyStatus).Must(x => x is null or "SUB" or "UNS" or "None");
            RuleFor(x => x.PriceMechanismGas).Must(x => x is null or "FIXED" or "VARIABLE" or "EGSI ETF" or "TTF" or "WITHIN-DAY MKT" or "BGO" or "PGO" or "THE");
            RuleFor(x => x.ContractType).Must(x => x is null or "External" or "Intercompany");
        }
    }
}

public sealed class DeactivateContractValidator : Validator<DeactivateContractRequest>
{
    public DeactivateContractValidator()
    {
        RuleFor(x => x.ContractId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Version).GreaterThan(0);
    }
}
