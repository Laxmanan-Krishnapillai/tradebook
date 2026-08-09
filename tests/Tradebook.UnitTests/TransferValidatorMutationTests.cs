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

public sealed class TransferValidatorMutationTests
{
    private static readonly DateOnly SupplyMonth = new(2025, 2, 1);
    private static readonly DateOnly StartDay = new(2025, 2, 10);

    [Fact]
    public void CreateAcceptsAValidRequestAndEqualDayBoundaries()
    {
        var request = Create(startDay: StartDay, endDay: StartDay);

        new CreateTransferValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void CreateAcceptsOmittedOptionalDatesAndEnums()
    {
        new CreateTransferValidator().Validate(Create()).ShouldBeValid();
    }

    [Fact]
    public void CreateAcceptsEitherOptionalDayWithoutTheOther()
    {
        var requests = new[] { Create(startDay: StartDay), Create(endDay: StartDay) };

        foreach (var request in requests)
            new CreateTransferValidator().Validate(request).ShouldBeValid(request.ToString());
    }

    [Fact]
    public void CreateRejectsAnEmptyContractId()
    {
        var request = Create(contractId: Guid.Empty);

        new CreateTransferValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(CreateTransferRequest.ContractId));
    }

    [Theory]
    [MemberData(nameof(InvalidSupplyMonths))]
    public void CreateRejectsDefaultOrNonMonthStartSupplyDates(DateOnly supplyMonth)
    {
        var request = Create(supplyMonth: supplyMonth);

        new CreateTransferValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(CreateTransferRequest.SupplyMonth));
    }

    public static TheoryData<DateOnly> InvalidSupplyMonths =>
        new() { default(DateOnly), new DateOnly(2025, 2, 2) };

    [Fact]
    public void CreateRejectsAnEndDayBeforeTheStartDay()
    {
        var request = Create(startDay: StartDay, endDay: StartDay.AddDays(-1));

        new CreateTransferValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(CreateTransferRequest.EndDay));
    }

    [Theory]
    [MemberData(nameof(GasPriceMechanisms))]
    public void CreateAcceptsEveryExactGasPriceMechanism(string priceMechanism)
    {
        var request = Create(priceMechanism: priceMechanism);

        new CreateTransferValidator().Validate(request).ShouldBeValid();
    }

    [Theory]
    [MemberData(nameof(ReportStatuses))]
    public void CreateAcceptsEveryExactReportStatus(string status)
    {
        var request = Create(status: status);

        new CreateTransferValidator().Validate(request).ShouldBeValid();
    }

    [Theory]
    [InlineData("")]
    [InlineData("fixed")]
    [InlineData("WITHIN DAY MKT")]
    public void CreateRejectsUnknownOrNonExactPriceMechanisms(string priceMechanism)
    {
        var request = Create(priceMechanism: priceMechanism);

        new CreateTransferValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(CreateTransferRequest.PriceMechanism));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Completed")]
    [InlineData("awaiting")]
    public void CreateRejectsUnknownOrNonExactStatuses(string status)
    {
        var request = Create(status: status);

        new CreateTransferValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(CreateTransferRequest.Status));
    }

    [Fact]
    public void UpdateRejectsAnEmptyId()
    {
        var request = EmptyUpdate() with { TransferId = Guid.Empty, TradingArea = "DK1" };

        new UpdateTransferValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(UpdateTransferRequest.TransferId));
    }

    [Fact]
    public void UpdateRejectsAnEmptyPatchWithTheDocumentedMessage()
    {
        new UpdateTransferValidator()
            .Validate(EmptyUpdate())
            .ShouldRejectRequest("At least one mutable field is required.");
    }

    [Fact]
    public void UpdateAcceptsEachMutableFieldIndependently()
    {
        var requests = new[]
        {
            EmptyUpdate() with
            {
                TradingArea = string.Empty,
            },
            EmptyUpdate() with
            {
                CapacityMw = 0m,
            },
            EmptyUpdate() with
            {
                BookedCapacityMw = 0m,
            },
            EmptyUpdate() with
            {
                VolumeMwh = 0m,
            },
            EmptyUpdate() with
            {
                BalancingEffectMwh = 0m,
            },
            EmptyUpdate() with
            {
                PriceMechanism = "FIXED",
            },
            EmptyUpdate() with
            {
                TransportCostEurMwh = 0m,
            },
            EmptyUpdate() with
            {
                CapacityCostEurMwh = 0m,
            },
            EmptyUpdate() with
            {
                Status = "Awaiting",
            },
            EmptyUpdate() with
            {
                Comments = string.Empty,
            },
        };

        foreach (var request in requests)
            new UpdateTransferValidator().Validate(request).ShouldBeValid(request.ToString());
    }

    [Theory]
    [MemberData(nameof(GasPriceMechanisms))]
    public void UpdateAcceptsEveryExactGasPriceMechanism(string priceMechanism)
    {
        var request = EmptyUpdate() with { PriceMechanism = priceMechanism };

        new UpdateTransferValidator().Validate(request).ShouldBeValid();
    }

    [Theory]
    [MemberData(nameof(ReportStatuses))]
    public void UpdateAcceptsEveryExactReportStatus(string status)
    {
        var request = EmptyUpdate() with { Status = status };

        new UpdateTransferValidator().Validate(request).ShouldBeValid();
    }

    [Theory]
    [InlineData("")]
    [InlineData("ttf")]
    [InlineData("WITHIN DAY MKT")]
    public void UpdateRejectsUnknownOrNonExactPriceMechanisms(string priceMechanism)
    {
        var request = EmptyUpdate() with { PriceMechanism = priceMechanism };

        new UpdateTransferValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(UpdateTransferRequest.PriceMechanism));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Completed")]
    [InlineData("issue")]
    public void UpdateRejectsUnknownOrNonExactStatuses(string status)
    {
        var request = EmptyUpdate() with { Status = status };

        new UpdateTransferValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(UpdateTransferRequest.Status));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UpdateRejectsNonPositiveVersions(long version)
    {
        var request = EmptyUpdate() with { TradingArea = "DK1", Version = version };

        new UpdateTransferValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(UpdateTransferRequest.Version));
    }

    [Fact]
    public void CancelAcceptsExactReasonAndVersionBoundaries()
    {
        var request = new CancelTransferRequest(Guid.NewGuid(), new string('x', 500), 1);

        new CancelTransferValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void CancelRejectsEachRequiredOrBoundedValueOutsideItsContract()
    {
        var id = Guid.NewGuid();
        var cases = new[]
        {
            (
                nameof(CancelTransferRequest.TransferId),
                new CancelTransferRequest(Guid.Empty, "reason", 1)
            ),
            (nameof(CancelTransferRequest.Reason), new CancelTransferRequest(id, null!, 1)),
            (nameof(CancelTransferRequest.Reason), new CancelTransferRequest(id, string.Empty, 1)),
            (nameof(CancelTransferRequest.Reason), new CancelTransferRequest(id, "   ", 1)),
            (
                nameof(CancelTransferRequest.Reason),
                new CancelTransferRequest(id, new string('x', 501), 1)
            ),
            (nameof(CancelTransferRequest.Version), new CancelTransferRequest(id, "reason", 0)),
            (nameof(CancelTransferRequest.Version), new CancelTransferRequest(id, "reason", -1)),
        };

        foreach (var (propertyName, request) in cases)
            new CancelTransferValidator().Validate(request).ShouldRejectProperty(propertyName);
    }

    public static TheoryData<string> GasPriceMechanisms =>
        new() { "FIXED", "VARIABLE", "EGSI ETF", "TTF", "WITHIN-DAY MKT", "BGO", "PGO", "THE" };

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

    private static CreateTransferRequest Create(
        Guid? contractId = null,
        DateOnly? supplyMonth = null,
        DateOnly? startDay = null,
        DateOnly? endDay = null,
        string? priceMechanism = null,
        string? status = null
    ) =>
        new(
            contractId ?? Guid.NewGuid(),
            supplyMonth ?? SupplyMonth,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            startDay,
            endDay,
            priceMechanism,
            null,
            null,
            status,
            null
        );

    private static UpdateTransferRequest EmptyUpdate() =>
        new(Guid.NewGuid(), null, null, null, null, null, null, null, null, null, null, 1);
}
