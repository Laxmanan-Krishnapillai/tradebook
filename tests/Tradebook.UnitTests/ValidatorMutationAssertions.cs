using AwesomeAssertions;
using FluentValidation.Results;
using Tradebook.Api.Features.Hedges;
using Tradebook.Api.Features.MarketPrices;
using Tradebook.Api.Features.PhysicalDeliveries.CreatePhysicalDelivery;
using Tradebook.Api.Features.PhysicalDeliveries.DeletePhysicalDelivery;
using Tradebook.Api.Features.PhysicalDeliveries.UpdatePhysicalDelivery;
using Tradebook.Api.Features.TaxTariffs;
using Tradebook.Api.Features.Transfers;
using Tradebook.Core.DTOs;

namespace Tradebook.UnitTests;

internal static class ValidatorMutationAssertions
{
    public static void ShouldBeValid(
        this ValidationResult result,
        string because = "the boundary is valid"
    )
    {
        result
            .IsValid.Should()
            .BeTrue(
                "because {0}; validation errors were {1}",
                because,
                string.Join(" | ", result.Errors.Select(error => error.ErrorMessage))
            );
        result.Errors.Should().BeEmpty();
    }

    public static void ShouldRejectProperty(this ValidationResult result, string propertyName)
    {
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == propertyName);
    }

    public static void ShouldRejectRequest(this ValidationResult result, string exactMessage)
    {
        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .ContainSingle(error =>
                error.PropertyName == string.Empty && error.ErrorMessage == exactMessage
            );
    }
}
