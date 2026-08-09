using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.CapacityBookings;

public sealed class CreateCapacityBookingValidator : Validator<CreateCapacityBookingRequest>
{
    public CreateCapacityBookingValidator()
    {
        RuleFor(x => x.ContractId).Must(id => id.Value != Guid.Empty);
        RuleFor(x => x.SupplyMonth).Must(x => x != default && x.Day == 1);
        RuleFor(x => x.ContractInstanceId)
            .MaximumLength(120)
            .When(x => x.ContractInstanceId is not null);
        RuleFor(x => x.PriceMechanism)
            .Must(x =>
                x
                    is null
                        or "GTF/THE - Yearly"
                        or "GTF/THE - Monthly"
                        or "THE/GTF - Yearly"
                        or "THE/GTF - Monthly"
            );
        RuleFor(x => x.EndDay)
            .GreaterThanOrEqualTo(x => x.StartDay)
            .When(x => x.StartDay.HasValue && x.EndDay.HasValue);
        RuleFor(x => x.CapacityMw).GreaterThanOrEqualTo(0).When(x => x.CapacityMw.HasValue);
    }
}
