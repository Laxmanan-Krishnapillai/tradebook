using FastEndpoints;
using FluentValidation;
using Tradebook.Api.Validation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.Biotickets;

public sealed class CreateBioticketValidator : Validator<CreateBioticketRequest>
{
    public CreateBioticketValidator()
    {
        RuleFor(x => x.ContractId).Must(id => id.Value != Guid.Empty);
        RuleFor(x => x.BookType).Must(x => x is "Sourcing" or "Sales");
        RuleFor(x => x.ContractMonth).Must(x => x != default && x.Day == 1);
        RuleFor(x => x.EndDay)
            .GreaterThanOrEqualTo(x => x.StartDay)
            .When(x => x.StartDay.HasValue && x.EndDay.HasValue);
        RuleFor(x => x.VolumeNominatedTon)
            .GreaterThanOrEqualTo(0)
            .When(x => x.VolumeNominatedTon.HasValue);
        RuleFor(x => x.VolumeRealisedTon)
            .GreaterThanOrEqualTo(0)
            .When(x => x.VolumeRealisedTon.HasValue);
        RuleFor(x => x.VolumeTon).GreaterThanOrEqualTo(0).When(x => x.VolumeTon.HasValue);
        RuleFor(x => x.Status).Must(DomainValueValidation.ReportStatus);
    }
}
