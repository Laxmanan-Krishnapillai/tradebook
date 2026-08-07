using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.CapacityBookings;

public sealed class CreateCapacityBookingValidator : Validator<CreateCapacityBookingRequest>
{
    public CreateCapacityBookingValidator()
    {
        RuleFor(x => x.ContractId).NotEmpty();
        RuleFor(x => x.SupplyMonth).Must(x => x != default && x.Day == 1);
        RuleFor(x => x.ContractInstanceId).MaximumLength(120).When(x => x.ContractInstanceId is not null);
        RuleFor(x => x.PriceMechanism).Must(x => x is null or "GTF/THE - Yearly" or "GTF/THE - Monthly" or "THE/GTF - Yearly" or "THE/GTF - Monthly");
        RuleFor(x => x.EndDay).GreaterThanOrEqualTo(x => x.StartDay).When(x => x.StartDay.HasValue && x.EndDay.HasValue);
        RuleFor(x => x.CapacityMw).GreaterThanOrEqualTo(0).When(x => x.CapacityMw.HasValue);
    }
}

public sealed class UpdateCapacityBookingValidator : Validator<UpdateCapacityBookingRequest>
{
    public UpdateCapacityBookingValidator()
    {
        RuleFor(x => x.CapacityBookingId).NotEmpty();
        RuleFor(x => x.Version).GreaterThan(0);
        RuleFor(x => x.PriceMechanism).Must(x => x is null or "GTF/THE - Yearly" or "GTF/THE - Monthly" or "THE/GTF - Yearly" or "THE/GTF - Monthly");
        RuleFor(x => x.EndDay).GreaterThanOrEqualTo(x => x.StartDay).When(x => x.StartDay.HasValue && x.EndDay.HasValue);
        RuleFor(x => x.CapacityMw).GreaterThanOrEqualTo(0).When(x => x.CapacityMw.HasValue);
        RuleFor(x => x)
            .Must(x => x.BalancingGroup is not null || x.PriceMechanism is not null ||
                       x.StartArea is not null || x.EndArea is not null ||
                       x.StartDay.HasValue || x.EndDay.HasValue || x.CapacityMw.HasValue ||
                       x.CapacityPriceEurMwh.HasValue || x.CapacityCostEur.HasValue ||
                       x.Comments is not null)
            .WithMessage("At least one mutable field is required.");
    }
}

public sealed class DeleteCapacityBookingValidator : Validator<DeleteCapacityBookingRequest>
{
    public DeleteCapacityBookingValidator()
    {
        RuleFor(x => x.CapacityBookingId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Version).GreaterThan(0);
    }
}
