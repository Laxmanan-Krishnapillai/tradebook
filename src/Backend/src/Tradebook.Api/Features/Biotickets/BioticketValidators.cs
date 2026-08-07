using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;
using Tradebook.Api.Validation;

namespace Tradebook.Api.Features.Biotickets;

public sealed class CreateBioticketValidator : Validator<CreateBioticketRequest>
{
    public CreateBioticketValidator()
    {
        RuleFor(x => x.ContractId).NotEmpty();
        RuleFor(x => x.BookType).Must(x => x is "Sourcing" or "Sales");
        RuleFor(x => x.ContractMonth).Must(x => x != default && x.Day == 1);
        RuleFor(x => x.EndDay).GreaterThanOrEqualTo(x => x.StartDay).When(x => x.StartDay.HasValue && x.EndDay.HasValue);
        RuleFor(x => x.VolumeNominatedTon).GreaterThanOrEqualTo(0).When(x => x.VolumeNominatedTon.HasValue);
        RuleFor(x => x.VolumeRealisedTon).GreaterThanOrEqualTo(0).When(x => x.VolumeRealisedTon.HasValue);
        RuleFor(x => x.VolumeTon).GreaterThanOrEqualTo(0).When(x => x.VolumeTon.HasValue);
        RuleFor(x => x.Status).Must(DomainValueValidation.ReportStatus);
    }
}

public sealed class UpdateBioticketValidator : Validator<UpdateBioticketRequest>
{
    public UpdateBioticketValidator()
    {
        RuleFor(x => x.BioticketId).NotEmpty();
        RuleFor(x => x.Version).GreaterThan(0);
        RuleFor(x => x.VolumeRealisedTon).GreaterThanOrEqualTo(0).When(x => x.VolumeRealisedTon.HasValue);
        RuleFor(x => x.VolumeTon).GreaterThanOrEqualTo(0).When(x => x.VolumeTon.HasValue);
        RuleFor(x => x.Status).Must(DomainValueValidation.ReportStatus);
        RuleFor(x => x)
            .Must(x => x.VolumeRealisedTon.HasValue || x.VolumeTon.HasValue ||
                       x.CostEurTon.HasValue || x.RevenueEur.HasValue || x.VatPct.HasValue ||
                       x.VatEur.HasValue || x.InvoiceAmountEur.HasValue ||
                       x.Status is not null || x.Comment is not null)
            .WithMessage("At least one mutable field is required.");
    }
}

public sealed class CancelBioticketValidator : Validator<CancelBioticketRequest>
{
    public CancelBioticketValidator()
    {
        RuleFor(x => x.BioticketId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Version).GreaterThan(0);
    }
}
