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

public sealed class PhysicalDeliveryValidatorMutationBoundaryTests
{
    private static readonly DateOnly SupplyMonth = new(2025, 2, 1);
    private static readonly DateOnly StartDay = new(2025, 2, 10);

    [Fact]
    public void CreateAcceptsExactInstanceDateAndNumericBoundaries()
    {
        var request = Create(
            instanceId: new string('x', 120),
            startDay: StartDay,
            endDay: StartDay,
            nominated: 0m,
            realised: 0m
        );

        new CreatePhysicalDeliveryValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void CreateAcceptsAnOmittedInstanceOptionalDatesAndOptionalNumericValues()
    {
        var request = Create(instanceId: null, nominated: null, realised: null);

        new CreatePhysicalDeliveryValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void CreateAcceptsEitherOptionalDayWithoutTheOther()
    {
        var requests = new[] { Create(startDay: StartDay), Create(endDay: StartDay) };

        foreach (var request in requests)
            new CreatePhysicalDeliveryValidator()
                .Validate(request)
                .ShouldBeValid(request.ToString());
    }

    [Fact]
    public void CreateRejectsAnEmptyContractId()
    {
        var request = Create(contractId: Guid.Empty);

        new CreatePhysicalDeliveryValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(CreatePhysicalDeliveryRequest.ContractId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateRejectsAnEmptySuppliedInstanceId(string instanceId)
    {
        var request = Create(instanceId: instanceId);

        new CreatePhysicalDeliveryValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(CreatePhysicalDeliveryRequest.ContractInstanceId));
    }

    [Fact]
    public void CreateRejectsAnInstanceIdAbove120Characters()
    {
        var request = Create(instanceId: new string('x', 121));

        new CreatePhysicalDeliveryValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(CreatePhysicalDeliveryRequest.ContractInstanceId));
    }

    [Theory]
    [InlineData("Sourcing")]
    [InlineData("Sales")]
    [InlineData("Intercompany")]
    public void CreateAcceptsEveryExactBookType(string bookType)
    {
        new CreatePhysicalDeliveryValidator().Validate(Create(bookType: bookType)).ShouldBeValid();
    }

    [Theory]
    [InlineData("")]
    [InlineData("sales")]
    [InlineData("Internal")]
    public void CreateRejectsUnknownOrNonExactBookTypes(string bookType)
    {
        new CreatePhysicalDeliveryValidator()
            .Validate(Create(bookType: bookType))
            .ShouldRejectProperty(nameof(CreatePhysicalDeliveryRequest.BookType));
    }

    [Theory]
    [MemberData(nameof(InvalidSupplyMonths))]
    public void CreateRejectsDefaultOrNonMonthStartSupplyDates(
        DateOnly supplyMonth,
        string expectedPropertyName
    )
    {
        new CreatePhysicalDeliveryValidator()
            .Validate(Create(supplyMonth: supplyMonth))
            .ShouldRejectProperty(expectedPropertyName);
    }

    public static TheoryData<DateOnly, string> InvalidSupplyMonths =>
        new()
        {
            { default(DateOnly), nameof(CreatePhysicalDeliveryRequest.SupplyMonth) },
            { new DateOnly(2025, 2, 2), "SupplyMonth.Day" },
        };

    [Fact]
    public void CreateRejectsAnEndDayBeforeTheStartDay()
    {
        var request = Create(startDay: StartDay, endDay: StartDay.AddDays(-1));

        new CreatePhysicalDeliveryValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(CreatePhysicalDeliveryRequest.EndDay));
    }

    [Fact]
    public void CreateRejectsEachBoundedNumericValueBelowZero()
    {
        var cases = new[]
        {
            (nameof(CreatePhysicalDeliveryRequest.VolumeNominatedMwh), Create(nominated: -0.01m)),
            (nameof(CreatePhysicalDeliveryRequest.VolumeRealisedMwh), Create(realised: -0.01m)),
        };

        foreach (var (propertyName, request) in cases)
            new CreatePhysicalDeliveryValidator()
                .Validate(request)
                .ShouldRejectProperty(propertyName);
    }

    [Fact]
    public void UpdateRejectsAnEmptyId()
    {
        var request = new UpdatePhysicalDeliveryRequest(Guid.Empty, 0m, null, 1);

        new UpdatePhysicalDeliveryValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(UpdatePhysicalDeliveryRequest.DeliveryId));
    }

    [Fact]
    public void UpdateRejectsAnEmptyPatchWithTheDocumentedMessage()
    {
        var request = new UpdatePhysicalDeliveryRequest(Guid.NewGuid(), null, null, 1);

        new UpdatePhysicalDeliveryValidator()
            .Validate(request)
            .ShouldRejectRequest("At least one mutable field is required.");
    }

    [Fact]
    public void UpdateAcceptsEachMutableFieldIndependentlyAtItsBoundary()
    {
        var requests = new[]
        {
            new UpdatePhysicalDeliveryRequest(Guid.NewGuid(), 0m, null, 1),
            new UpdatePhysicalDeliveryRequest(Guid.NewGuid(), null, "Awaiting", 1),
        };

        foreach (var request in requests)
            new UpdatePhysicalDeliveryValidator()
                .Validate(request)
                .ShouldBeValid(request.ToString());
    }

    [Fact]
    public void UpdateRejectsRealisedVolumeBelowZero()
    {
        var request = new UpdatePhysicalDeliveryRequest(Guid.NewGuid(), -0.01m, null, 1);

        new UpdatePhysicalDeliveryValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(UpdatePhysicalDeliveryRequest.VolumeRealisedMwh));
    }

    [Theory]
    [MemberData(nameof(ReportStatuses))]
    public void UpdateAcceptsEveryExactReportStatus(string status)
    {
        var request = new UpdatePhysicalDeliveryRequest(Guid.NewGuid(), null, status, 1);

        new UpdatePhysicalDeliveryValidator().Validate(request).ShouldBeValid();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Completed")]
    [InlineData("awaiting")]
    public void UpdateRejectsUnknownOrNonExactReportStatuses(string status)
    {
        var request = new UpdatePhysicalDeliveryRequest(Guid.NewGuid(), null, status, 1);

        new UpdatePhysicalDeliveryValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(UpdatePhysicalDeliveryRequest.Status));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UpdateRejectsNonPositiveVersions(long version)
    {
        var request = new UpdatePhysicalDeliveryRequest(Guid.NewGuid(), 0m, null, version);

        new UpdatePhysicalDeliveryValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(UpdatePhysicalDeliveryRequest.Version));
    }

    [Fact]
    public void DeleteAcceptsExactReasonAndVersionBoundaries()
    {
        var request = new DeletePhysicalDeliveryRequest(Guid.NewGuid(), new string('x', 500), 1);

        new DeletePhysicalDeliveryValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void DeleteRejectsEachRequiredOrBoundedValueOutsideItsContract()
    {
        var id = Guid.NewGuid();
        var cases = new[]
        {
            (
                nameof(DeletePhysicalDeliveryRequest.DeliveryId),
                new DeletePhysicalDeliveryRequest(Guid.Empty, "reason", 1)
            ),
            (
                nameof(DeletePhysicalDeliveryRequest.Reason),
                new DeletePhysicalDeliveryRequest(id, null!, 1)
            ),
            (
                nameof(DeletePhysicalDeliveryRequest.Reason),
                new DeletePhysicalDeliveryRequest(id, string.Empty, 1)
            ),
            (
                nameof(DeletePhysicalDeliveryRequest.Reason),
                new DeletePhysicalDeliveryRequest(id, "   ", 1)
            ),
            (
                nameof(DeletePhysicalDeliveryRequest.Reason),
                new DeletePhysicalDeliveryRequest(id, new string('x', 501), 1)
            ),
            (
                nameof(DeletePhysicalDeliveryRequest.Version),
                new DeletePhysicalDeliveryRequest(id, "reason", 0)
            ),
            (
                nameof(DeletePhysicalDeliveryRequest.Version),
                new DeletePhysicalDeliveryRequest(id, "reason", -1)
            ),
        };

        foreach (var (propertyName, request) in cases)
            new DeletePhysicalDeliveryValidator()
                .Validate(request)
                .ShouldRejectProperty(propertyName);
    }

    public static TheoryData<string> ReportStatuses =>
        new()
        {
            "Completed - Payment Received/Sent",
            "In Progress - Invoice Received/Sent",
            "Pending - No Invoice",
            "Cancelled",
            "Awaiting",
            "Issue",
        };

    private static CreatePhysicalDeliveryRequest Create(
        Guid? contractId = null,
        string? instanceId = "instance-1",
        string bookType = "Sales",
        DateOnly? supplyMonth = null,
        DateOnly? startDay = null,
        DateOnly? endDay = null,
        decimal? nominated = 1m,
        decimal? realised = 1m
    ) =>
        new(
            contractId ?? Guid.NewGuid(),
            instanceId,
            bookType,
            supplyMonth ?? SupplyMonth,
            null,
            nominated,
            realised,
            null,
            startDay,
            endDay
        );
}
