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

public sealed class HedgeValidatorMutationTests
{
    private static readonly DateOnly MonthStart = new(2025, 2, 1);

    [Fact]
    public void CreateAcceptsTheExactMonthAndNumericLowerBoundaries()
    {
        var request = new CreateHedgeRequest(Guid.NewGuid(), MonthStart, 0m, 0m);

        new CreateHedgeValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void CreateAcceptsOmittedOptionalNumericValues()
    {
        var request = new CreateHedgeRequest(Guid.NewGuid(), MonthStart, null, null);

        new CreateHedgeValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void CreateRejectsAnEmptyContractId()
    {
        var request = new CreateHedgeRequest(Guid.Empty, MonthStart, 1m, 1m);

        new CreateHedgeValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(CreateHedgeRequest.ContractId));
    }

    [Theory]
    [MemberData(nameof(InvalidMonths))]
    public void CreateRejectsDefaultOrNonMonthStartDates(DateOnly month)
    {
        var request = new CreateHedgeRequest(Guid.NewGuid(), month, 1m, 1m);

        new CreateHedgeValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(CreateHedgeRequest.Month));
    }

    public static TheoryData<DateOnly> InvalidMonths =>
        new() { default(DateOnly), new DateOnly(2025, 2, 2) };

    [Fact]
    public void CreateRejectsAnAmountBelowZero()
    {
        var request = new CreateHedgeRequest(Guid.NewGuid(), MonthStart, -0.01m, 1m);

        new CreateHedgeValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(CreateHedgeRequest.HedgeAmountMwh));
    }

    [Fact]
    public void CreateRejectsAPriceBelowZero()
    {
        var request = new CreateHedgeRequest(Guid.NewGuid(), MonthStart, 1m, -0.01m);

        new CreateHedgeValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(CreateHedgeRequest.HedgePriceEurMwh));
    }

    [Fact]
    public void UpdateRejectsAnEmptyId()
    {
        var request = new UpdateHedgeRequest(Guid.Empty, 0m, null, 1);

        new UpdateHedgeValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(UpdateHedgeRequest.HedgeId));
    }

    [Fact]
    public void UpdateRejectsAnEmptyPatchWithTheDocumentedMessage()
    {
        var request = new UpdateHedgeRequest(Guid.NewGuid(), null, null, 1);

        new UpdateHedgeValidator()
            .Validate(request)
            .ShouldRejectRequest("At least one mutable field is required.");
    }

    [Fact]
    public void UpdateAcceptsEachMutableFieldIndependentlyAtZero()
    {
        var requests = new[]
        {
            new UpdateHedgeRequest(Guid.NewGuid(), 0m, null, 1),
            new UpdateHedgeRequest(Guid.NewGuid(), null, 0m, 1),
        };

        foreach (var request in requests)
            new UpdateHedgeValidator().Validate(request).ShouldBeValid(request.ToString());
    }

    [Fact]
    public void UpdateRejectsEachNumericValueBelowZero()
    {
        var cases = new[]
        {
            (
                nameof(UpdateHedgeRequest.HedgeAmountMwh),
                new UpdateHedgeRequest(Guid.NewGuid(), -0.01m, null, 1)
            ),
            (
                nameof(UpdateHedgeRequest.HedgePriceEurMwh),
                new UpdateHedgeRequest(Guid.NewGuid(), null, -0.01m, 1)
            ),
        };

        foreach (var (propertyName, request) in cases)
            new UpdateHedgeValidator().Validate(request).ShouldRejectProperty(propertyName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UpdateRejectsNonPositiveVersions(long version)
    {
        var request = new UpdateHedgeRequest(Guid.NewGuid(), 0m, null, version);

        new UpdateHedgeValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(UpdateHedgeRequest.Version));
    }

    [Fact]
    public void DeleteAcceptsExactReasonAndVersionBoundaries()
    {
        var request = new DeleteHedgeRequest(Guid.NewGuid(), new string('x', 500), 1);

        new DeleteHedgeValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void DeleteRejectsEachRequiredOrBoundedValueOutsideItsContract()
    {
        var id = Guid.NewGuid();
        var cases = new[]
        {
            (nameof(DeleteHedgeRequest.HedgeId), new DeleteHedgeRequest(Guid.Empty, "reason", 1)),
            (nameof(DeleteHedgeRequest.Reason), new DeleteHedgeRequest(id, null!, 1)),
            (nameof(DeleteHedgeRequest.Reason), new DeleteHedgeRequest(id, string.Empty, 1)),
            (nameof(DeleteHedgeRequest.Reason), new DeleteHedgeRequest(id, "   ", 1)),
            (
                nameof(DeleteHedgeRequest.Reason),
                new DeleteHedgeRequest(id, new string('x', 501), 1)
            ),
            (nameof(DeleteHedgeRequest.Version), new DeleteHedgeRequest(id, "reason", 0)),
            (nameof(DeleteHedgeRequest.Version), new DeleteHedgeRequest(id, "reason", -1)),
        };

        foreach (var (propertyName, request) in cases)
            new DeleteHedgeValidator().Validate(request).ShouldRejectProperty(propertyName);
    }
}
