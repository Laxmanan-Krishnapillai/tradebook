using FastEndpoints;
using FluentValidation;
using Tradebook.Api.Validation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.Biotickets;

public sealed class UpdateBioticketValidator : Validator<UpdateBioticketRequest>
{
    public UpdateBioticketValidator()
    {
        RuleFor(x => x.BioticketId).NotEmpty();
        RuleFor(x => x.Version).GreaterThan(0);
        RuleFor(x => x.VolumeRealisedTon)
            .GreaterThanOrEqualTo(0)
            .When(x => x.VolumeRealisedTon.HasValue);
        RuleFor(x => x.VolumeTon).GreaterThanOrEqualTo(0).When(x => x.VolumeTon.HasValue);
        RuleFor(x => x.Status).Must(DomainValueValidation.ReportStatus);
        RuleFor(x => x)
            .Must(x =>
                x.VolumeRealisedTon.HasValue
                || x.VolumeTon.HasValue
                || x.CostEurTon.HasValue
                || x.RevenueEur.HasValue
                || x.VatPct.HasValue
                || x.VatEur.HasValue
                || x.InvoiceAmountEur.HasValue
                || x.Status is not null
                || x.Comment is not null
            )
            .WithMessage("At least one mutable field is required.");
    }
}
