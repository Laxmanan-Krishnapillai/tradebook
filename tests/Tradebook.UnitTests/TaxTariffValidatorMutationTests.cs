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

public sealed class TaxTariffValidatorMutationTests
{
    private static readonly DateOnly PeriodStart = new(2025, 1, 1);

    [Fact]
    public void CreateAcceptsEqualPeriodDatesAndAnExactCurrency()
    {
        var request = Create(periodEnd: PeriodStart, currency: "EUR");

        new CreateTaxTariffValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void CreateRejectsAnEmptyContractId()
    {
        var request = Create(contractId: Guid.Empty);

        new CreateTaxTariffValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(CreateTaxTariffRequest.ContractId));
    }

    [Fact]
    public void CreateRejectsTheDefaultPeriodStart()
    {
        var request = Create(periodStart: new DateOnly(), periodEnd: new DateOnly(2025, 1, 1));

        new CreateTaxTariffValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(CreateTaxTariffRequest.PeriodStart));
    }

    [Fact]
    public void CreateRejectsAPeriodEndBeforeTheStart()
    {
        var request = Create(periodEnd: PeriodStart.AddDays(-1));

        new CreateTaxTariffValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(CreateTaxTariffRequest.PeriodEnd));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("eur")]
    [InlineData("EU1")]
    public void CreateRejectsCurrencyOutsideTheExactThreeUppercaseLetterContract(string? currency)
    {
        var request = Create(currency: currency!);

        new CreateTaxTariffValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(CreateTaxTariffRequest.Currency));
    }

    [Fact]
    public void UpdateAcceptsNullableRatesOmittedWithRequiredCurrencyAndVersionBoundaries()
    {
        var request = Update(currency: "DKK", version: 1);

        new UpdateTaxTariffValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void UpdateRejectsAnEmptyId()
    {
        var request = Update(taxTariffId: Guid.Empty);

        new UpdateTaxTariffValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(UpdateTaxTariffRequest.TaxTariffId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("eur")]
    [InlineData("E1R")]
    public void UpdateRejectsCurrencyOutsideTheExactThreeUppercaseLetterContract(string? currency)
    {
        var request = Update(currency: currency!);

        new UpdateTaxTariffValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(UpdateTaxTariffRequest.Currency));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UpdateRejectsNonPositiveVersions(long version)
    {
        var request = Update(version: version);

        new UpdateTaxTariffValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(UpdateTaxTariffRequest.Version));
    }

    [Fact]
    public void DeleteAcceptsExactReasonAndVersionBoundaries()
    {
        var request = new DeleteTaxTariffRequest(Guid.NewGuid(), new string('x', 500), 1);

        new DeleteTaxTariffValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void DeleteRejectsEachRequiredOrBoundedValueOutsideItsContract()
    {
        var id = Guid.NewGuid();
        var cases = new[]
        {
            (
                nameof(DeleteTaxTariffRequest.TaxTariffId),
                new DeleteTaxTariffRequest(Guid.Empty, "reason", 1)
            ),
            (nameof(DeleteTaxTariffRequest.Reason), new DeleteTaxTariffRequest(id, null!, 1)),
            (
                nameof(DeleteTaxTariffRequest.Reason),
                new DeleteTaxTariffRequest(id, string.Empty, 1)
            ),
            (nameof(DeleteTaxTariffRequest.Reason), new DeleteTaxTariffRequest(id, "   ", 1)),
            (
                nameof(DeleteTaxTariffRequest.Reason),
                new DeleteTaxTariffRequest(id, new string('x', 501), 1)
            ),
            (nameof(DeleteTaxTariffRequest.Version), new DeleteTaxTariffRequest(id, "reason", 0)),
            (nameof(DeleteTaxTariffRequest.Version), new DeleteTaxTariffRequest(id, "reason", -1)),
        };

        foreach (var (propertyName, request) in cases)
            new DeleteTaxTariffValidator().Validate(request).ShouldRejectProperty(propertyName);
    }

    private static CreateTaxTariffRequest Create(
        Guid? contractId = null,
        DateOnly? periodStart = null,
        DateOnly? periodEnd = null,
        string currency = "EUR"
    ) =>
        new(
            contractId ?? Guid.NewGuid(),
            null,
            periodStart ?? PeriodStart,
            periodEnd ?? PeriodStart.AddMonths(1),
            null,
            null,
            null,
            null,
            null,
            null,
            currency
        );

    private static UpdateTaxTariffRequest Update(
        Guid? taxTariffId = null,
        string currency = "EUR",
        long version = 1
    ) => new(taxTariffId ?? Guid.NewGuid(), null, null, null, null, null, null, currency, version);
}
