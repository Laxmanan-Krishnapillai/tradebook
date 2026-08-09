using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.PhysicalDeliveries.CreatePhysicalDelivery;

public sealed class CreatePhysicalDeliveryValidator : Validator<CreatePhysicalDeliveryRequest>
{
    public CreatePhysicalDeliveryValidator()
    {
        RuleFor(request => request.ContractId).Must(id => id.Value != Guid.Empty);
        RuleFor(request => request.ContractInstanceId).NotEmpty().MaximumLength(120)
            .When(request => request.ContractInstanceId is not null);
        RuleFor(request => request.BookType).Must(value => value is "Sourcing" or "Sales" or "Intercompany");
        RuleFor(request => request.SupplyMonth).NotEqual(default(DateOnly));
        RuleFor(request => request.SupplyMonth.Day).Equal(1);
        RuleFor(request => request.EndDay)
            .GreaterThanOrEqualTo(request => request.StartDay)
            .When(request => request.StartDay.HasValue && request.EndDay.HasValue);
        RuleFor(request => request.VolumeNominatedMwh).GreaterThanOrEqualTo(0).When(request => request.VolumeNominatedMwh.HasValue);
        RuleFor(request => request.VolumeRealisedMwh).GreaterThanOrEqualTo(0).When(request => request.VolumeRealisedMwh.HasValue);
    }
}
