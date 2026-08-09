using FastEndpoints;
using FluentValidation;
using Tradebook.Api.Validation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.Transfers;

public sealed class UpdateTransferValidator : Validator<UpdateTransferRequest>
{
    public UpdateTransferValidator()
    {
        RuleFor(x => x.TransferId).Must(id => id.Value != Guid.Empty);
        RuleFor(x => x.Version).GreaterThan(0);
        RuleFor(x => x.PriceMechanism).Must(DomainValueValidation.GasPriceMechanism);
        RuleFor(x => x.Status).Must(DomainValueValidation.ReportStatus);
        RuleFor(x => x)
            .Must(x =>
                x.TradingArea is not null
                || x.CapacityMw.HasValue
                || x.BookedCapacityMw.HasValue
                || x.VolumeMwh.HasValue
                || x.BalancingEffectMwh.HasValue
                || x.PriceMechanism is not null
                || x.TransportCostEurMwh.HasValue
                || x.CapacityCostEurMwh.HasValue
                || x.Status is not null
                || x.Comments is not null
            )
            .WithMessage("At least one mutable field is required.");
    }
}
