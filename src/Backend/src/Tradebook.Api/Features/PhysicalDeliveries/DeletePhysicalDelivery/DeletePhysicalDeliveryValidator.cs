using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.PhysicalDeliveries.DeletePhysicalDelivery;

public sealed class DeletePhysicalDeliveryValidator : Validator<DeletePhysicalDeliveryRequest>
{
    public DeletePhysicalDeliveryValidator()
    {
        RuleFor(request => request.DeliveryId).NotEmpty();
        RuleFor(request => request.Reason).NotEmpty().MaximumLength(500);
        RuleFor(request => request.Version).GreaterThan(0);
    }
}
