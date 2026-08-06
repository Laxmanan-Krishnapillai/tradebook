using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.PhysicalDeliveries.UpdatePhysicalDelivery;

public sealed class UpdatePhysicalDeliveryValidator : Validator<UpdatePhysicalDeliveryRequest>
{
    public UpdatePhysicalDeliveryValidator()
    {
        RuleFor(request => request.DeliveryId).NotEmpty();
        RuleFor(request => request.Version).GreaterThan(0);
        RuleFor(request => request.Status).Must(value => value is null or "Completed - Payment Received/Sent" or "In Progress - Invoice Received/Sent" or "Pending - No Invoice" or "Cancelled" or "Awaiting" or "Issue");
        RuleFor(request => request.VolumeRealisedMwh).GreaterThanOrEqualTo(0).When(request => request.VolumeRealisedMwh.HasValue);
    }
}
