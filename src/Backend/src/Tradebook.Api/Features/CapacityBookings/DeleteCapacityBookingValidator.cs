using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.CapacityBookings;

public sealed class DeleteCapacityBookingValidator : Validator<DeleteCapacityBookingRequest>
{
    public DeleteCapacityBookingValidator()
    {
        RuleFor(x => x.CapacityBookingId).Must(id => id.Value != Guid.Empty);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Version).GreaterThan(0);
    }
}
