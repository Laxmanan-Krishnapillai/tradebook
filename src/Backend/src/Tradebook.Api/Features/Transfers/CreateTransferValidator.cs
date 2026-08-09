using FastEndpoints;
using FluentValidation;
using Tradebook.Api.Validation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.Transfers;

public sealed class CreateTransferValidator : Validator<CreateTransferRequest>
{
    public CreateTransferValidator()
    {
        RuleFor(x => x.ContractId).Must(id => id.Value != Guid.Empty);
        RuleFor(x => x.SupplyMonth).Must(x => x != default && x.Day == 1);
        RuleFor(x => x.PriceMechanism).Must(DomainValueValidation.GasPriceMechanism);
        RuleFor(x => x.Status).Must(DomainValueValidation.ReportStatus);
        RuleFor(x => x.EndDay)
            .GreaterThanOrEqualTo(x => x.StartDay)
            .When(x => x.StartDay.HasValue && x.EndDay.HasValue);
    }
}
