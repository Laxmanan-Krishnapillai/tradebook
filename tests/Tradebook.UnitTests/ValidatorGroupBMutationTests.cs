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
    public void Create_accepts_the_exact_month_and_numeric_lower_boundaries()
    {
        var request = new CreateHedgeRequest(Guid.NewGuid(), MonthStart, 0m, 0m);

        new CreateHedgeValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void Create_accepts_omitted_optional_numeric_values()
    {
        var request = new CreateHedgeRequest(Guid.NewGuid(), MonthStart, null, null);

        new CreateHedgeValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void Create_rejects_an_empty_contract_id()
    {
        var request = new CreateHedgeRequest(Guid.Empty, MonthStart, 1m, 1m);

        new CreateHedgeValidator().Validate(request)
            .ShouldRejectProperty(nameof(CreateHedgeRequest.ContractId));
    }

    [Theory]
    [MemberData(nameof(InvalidMonths))]
    public void Create_rejects_default_or_non_month_start_dates(DateOnly month)
    {
        var request = new CreateHedgeRequest(Guid.NewGuid(), month, 1m, 1m);

        new CreateHedgeValidator().Validate(request)
            .ShouldRejectProperty(nameof(CreateHedgeRequest.Month));
    }

    public static TheoryData<DateOnly> InvalidMonths => new()
    {
        default(DateOnly),
        new DateOnly(2025, 2, 2),
    };

    [Fact]
    public void Create_rejects_an_amount_below_zero()
    {
        var request = new CreateHedgeRequest(Guid.NewGuid(), MonthStart, -0.01m, 1m);

        new CreateHedgeValidator().Validate(request)
            .ShouldRejectProperty(nameof(CreateHedgeRequest.HedgeAmountMwh));
    }

    [Fact]
    public void Create_rejects_a_price_below_zero()
    {
        var request = new CreateHedgeRequest(Guid.NewGuid(), MonthStart, 1m, -0.01m);

        new CreateHedgeValidator().Validate(request)
            .ShouldRejectProperty(nameof(CreateHedgeRequest.HedgePriceEurMwh));
    }

    [Fact]
    public void Update_rejects_an_empty_id()
    {
        var request = new UpdateHedgeRequest(Guid.Empty, 0m, null, 1);

        new UpdateHedgeValidator().Validate(request)
            .ShouldRejectProperty(nameof(UpdateHedgeRequest.HedgeId));
    }

    [Fact]
    public void Update_rejects_an_empty_patch_with_the_documented_message()
    {
        var request = new UpdateHedgeRequest(Guid.NewGuid(), null, null, 1);

        new UpdateHedgeValidator().Validate(request)
            .ShouldRejectRequest("At least one mutable field is required.");
    }

    [Fact]
    public void Update_accepts_each_mutable_field_independently_at_zero()
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
    public void Update_rejects_each_numeric_value_below_zero()
    {
        var cases = new[]
        {
            (nameof(UpdateHedgeRequest.HedgeAmountMwh), new UpdateHedgeRequest(Guid.NewGuid(), -0.01m, null, 1)),
            (nameof(UpdateHedgeRequest.HedgePriceEurMwh), new UpdateHedgeRequest(Guid.NewGuid(), null, -0.01m, 1)),
        };

        foreach (var (propertyName, request) in cases)
            new UpdateHedgeValidator().Validate(request).ShouldRejectProperty(propertyName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Update_rejects_non_positive_versions(long version)
    {
        var request = new UpdateHedgeRequest(Guid.NewGuid(), 0m, null, version);

        new UpdateHedgeValidator().Validate(request)
            .ShouldRejectProperty(nameof(UpdateHedgeRequest.Version));
    }

    [Fact]
    public void Delete_accepts_exact_reason_and_version_boundaries()
    {
        var request = new DeleteHedgeRequest(Guid.NewGuid(), new string('x', 500), 1);

        new DeleteHedgeValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void Delete_rejects_each_required_or_bounded_value_outside_its_contract()
    {
        var id = Guid.NewGuid();
        var cases = new[]
        {
            (nameof(DeleteHedgeRequest.HedgeId), new DeleteHedgeRequest(Guid.Empty, "reason", 1)),
            (nameof(DeleteHedgeRequest.Reason), new DeleteHedgeRequest(id, null!, 1)),
            (nameof(DeleteHedgeRequest.Reason), new DeleteHedgeRequest(id, string.Empty, 1)),
            (nameof(DeleteHedgeRequest.Reason), new DeleteHedgeRequest(id, "   ", 1)),
            (nameof(DeleteHedgeRequest.Reason), new DeleteHedgeRequest(id, new string('x', 501), 1)),
            (nameof(DeleteHedgeRequest.Version), new DeleteHedgeRequest(id, "reason", 0)),
            (nameof(DeleteHedgeRequest.Version), new DeleteHedgeRequest(id, "reason", -1)),
        };

        foreach (var (propertyName, request) in cases)
            new DeleteHedgeValidator().Validate(request).ShouldRejectProperty(propertyName);
    }
}

public sealed class MarketPriceValidatorMutationTests
{
    private static readonly DateOnly PriceDate = new(2025, 2, 14);

    [Fact]
    public void Upsert_accepts_each_market_or_fx_value_as_the_only_supplied_value()
    {
        var requests = new[]
        {
            EmptyUpsert() with { TtfEurMwh = 1m },
            EmptyUpsert() with { EgsiEtfEurMwh = 1m },
            EmptyUpsert() with { TheEurMwh = 1m },
            EmptyUpsert() with { BgoEurMwh = 1m },
            EmptyUpsert() with { PgoEurMwh = 1m },
            EmptyUpsert() with { EuaEurMwh = 1m },
            EmptyUpsert() with { WithinDayMktEurMwh = 1m },
            EmptyUpsert() with { EurSek = 1m },
            EmptyUpsert() with { EurChf = 1m },
            EmptyUpsert() with { EurGbp = 1m },
            EmptyUpsert() with { EurUsd = 1m },
            EmptyUpsert() with { EurDkk = 1m },
        };

        foreach (var request in requests)
            new UpsertMarketPriceValidator().Validate(request).ShouldBeValid(request.ToString());
    }

    [Fact]
    public void Upsert_rejects_a_request_without_any_market_or_fx_value()
    {
        new UpsertMarketPriceValidator().Validate(EmptyUpsert())
            .ShouldRejectRequest("At least one market or FX value is required.");
    }

    [Fact]
    public void Upsert_rejects_the_default_price_date()
    {
        var request = EmptyUpsert() with { PriceDate = default, TtfEurMwh = 1m };

        new UpsertMarketPriceValidator().Validate(request)
            .ShouldRejectProperty(nameof(UpsertMarketPriceRequest.PriceDate));
    }

    [Fact]
    public void Upsert_accepts_zero_as_the_version_lower_boundary()
    {
        var request = EmptyUpsert() with { TtfEurMwh = 1m, Version = 0 };

        new UpsertMarketPriceValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void Upsert_rejects_a_version_below_zero()
    {
        var request = EmptyUpsert() with { TtfEurMwh = 1m, Version = -1 };

        new UpsertMarketPriceValidator().Validate(request)
            .ShouldRejectProperty(nameof(UpsertMarketPriceRequest.Version));
    }

    [Fact]
    public void Upsert_accepts_negative_energy_market_prices()
    {
        var request = EmptyUpsert() with { TtfEurMwh = -0.01m };

        new UpsertMarketPriceValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void Upsert_accepts_each_fx_rate_above_zero()
    {
        var requests = FxRequests(0.01m);

        foreach (var request in requests)
            new UpsertMarketPriceValidator().Validate(request).ShouldBeValid(request.ToString());
    }

    [Fact]
    public void Upsert_rejects_each_fx_rate_at_zero()
    {
        var cases = new[]
        {
            (nameof(UpsertMarketPriceRequest.EurSek), EmptyUpsert() with { EurSek = 0m }),
            (nameof(UpsertMarketPriceRequest.EurChf), EmptyUpsert() with { EurChf = 0m }),
            (nameof(UpsertMarketPriceRequest.EurGbp), EmptyUpsert() with { EurGbp = 0m }),
            (nameof(UpsertMarketPriceRequest.EurUsd), EmptyUpsert() with { EurUsd = 0m }),
            (nameof(UpsertMarketPriceRequest.EurDkk), EmptyUpsert() with { EurDkk = 0m }),
        };

        foreach (var (propertyName, request) in cases)
            new UpsertMarketPriceValidator().Validate(request).ShouldRejectProperty(propertyName);
    }

    [Fact]
    public void Upsert_rejects_each_fx_rate_below_zero()
    {
        var cases = new[]
        {
            (nameof(UpsertMarketPriceRequest.EurSek), EmptyUpsert() with { EurSek = -0.01m }),
            (nameof(UpsertMarketPriceRequest.EurChf), EmptyUpsert() with { EurChf = -0.01m }),
            (nameof(UpsertMarketPriceRequest.EurGbp), EmptyUpsert() with { EurGbp = -0.01m }),
            (nameof(UpsertMarketPriceRequest.EurUsd), EmptyUpsert() with { EurUsd = -0.01m }),
            (nameof(UpsertMarketPriceRequest.EurDkk), EmptyUpsert() with { EurDkk = -0.01m }),
        };

        foreach (var (propertyName, request) in cases)
            new UpsertMarketPriceValidator().Validate(request).ShouldRejectProperty(propertyName);
    }

    [Fact]
    public void Delete_accepts_exact_reason_and_version_boundaries()
    {
        var request = new DeleteMarketPriceRequest(PriceDate, new string('x', 500), 1);

        new DeleteMarketPriceValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void Delete_rejects_each_required_or_bounded_value_outside_its_contract()
    {
        var cases = new[]
        {
            (nameof(DeleteMarketPriceRequest.PriceDate), new DeleteMarketPriceRequest(default, "reason", 1)),
            (nameof(DeleteMarketPriceRequest.Reason), new DeleteMarketPriceRequest(PriceDate, null!, 1)),
            (nameof(DeleteMarketPriceRequest.Reason), new DeleteMarketPriceRequest(PriceDate, string.Empty, 1)),
            (nameof(DeleteMarketPriceRequest.Reason), new DeleteMarketPriceRequest(PriceDate, "   ", 1)),
            (nameof(DeleteMarketPriceRequest.Reason), new DeleteMarketPriceRequest(PriceDate, new string('x', 501), 1)),
            (nameof(DeleteMarketPriceRequest.Version), new DeleteMarketPriceRequest(PriceDate, "reason", 0)),
            (nameof(DeleteMarketPriceRequest.Version), new DeleteMarketPriceRequest(PriceDate, "reason", -1)),
        };

        foreach (var (propertyName, request) in cases)
            new DeleteMarketPriceValidator().Validate(request).ShouldRejectProperty(propertyName);
    }

    private static UpsertMarketPriceRequest EmptyUpsert() => new(
        PriceDate,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        0);

    private static UpsertMarketPriceRequest[] FxRequests(decimal value) =>
    [
        EmptyUpsert() with { EurSek = value },
        EmptyUpsert() with { EurChf = value },
        EmptyUpsert() with { EurGbp = value },
        EmptyUpsert() with { EurUsd = value },
        EmptyUpsert() with { EurDkk = value },
    ];
}

public sealed class TaxTariffValidatorMutationTests
{
    private static readonly DateOnly PeriodStart = new(2025, 1, 1);

    [Fact]
    public void Create_accepts_equal_period_dates_and_an_exact_currency()
    {
        var request = Create(periodEnd: PeriodStart, currency: "EUR");

        new CreateTaxTariffValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void Create_rejects_an_empty_contract_id()
    {
        var request = Create(contractId: Guid.Empty);

        new CreateTaxTariffValidator().Validate(request)
            .ShouldRejectProperty(nameof(CreateTaxTariffRequest.ContractId));
    }

    [Fact]
    public void Create_rejects_the_default_period_start()
    {
        var request = Create(periodStart: new DateOnly(), periodEnd: new DateOnly(2025, 1, 1));

        new CreateTaxTariffValidator().Validate(request)
            .ShouldRejectProperty(nameof(CreateTaxTariffRequest.PeriodStart));
    }

    [Fact]
    public void Create_rejects_a_period_end_before_the_start()
    {
        var request = Create(periodEnd: PeriodStart.AddDays(-1));

        new CreateTaxTariffValidator().Validate(request)
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
    public void Create_rejects_currency_outside_the_exact_three_uppercase_letter_contract(string? currency)
    {
        var request = Create(currency: currency!);

        new CreateTaxTariffValidator().Validate(request)
            .ShouldRejectProperty(nameof(CreateTaxTariffRequest.Currency));
    }

    [Fact]
    public void Update_accepts_nullable_rates_omitted_with_required_currency_and_version_boundaries()
    {
        var request = Update(currency: "DKK", version: 1);

        new UpdateTaxTariffValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void Update_rejects_an_empty_id()
    {
        var request = Update(taxTariffId: Guid.Empty);

        new UpdateTaxTariffValidator().Validate(request)
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
    public void Update_rejects_currency_outside_the_exact_three_uppercase_letter_contract(string? currency)
    {
        var request = Update(currency: currency!);

        new UpdateTaxTariffValidator().Validate(request)
            .ShouldRejectProperty(nameof(UpdateTaxTariffRequest.Currency));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Update_rejects_non_positive_versions(long version)
    {
        var request = Update(version: version);

        new UpdateTaxTariffValidator().Validate(request)
            .ShouldRejectProperty(nameof(UpdateTaxTariffRequest.Version));
    }

    [Fact]
    public void Delete_accepts_exact_reason_and_version_boundaries()
    {
        var request = new DeleteTaxTariffRequest(Guid.NewGuid(), new string('x', 500), 1);

        new DeleteTaxTariffValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void Delete_rejects_each_required_or_bounded_value_outside_its_contract()
    {
        var id = Guid.NewGuid();
        var cases = new[]
        {
            (nameof(DeleteTaxTariffRequest.TaxTariffId), new DeleteTaxTariffRequest(Guid.Empty, "reason", 1)),
            (nameof(DeleteTaxTariffRequest.Reason), new DeleteTaxTariffRequest(id, null!, 1)),
            (nameof(DeleteTaxTariffRequest.Reason), new DeleteTaxTariffRequest(id, string.Empty, 1)),
            (nameof(DeleteTaxTariffRequest.Reason), new DeleteTaxTariffRequest(id, "   ", 1)),
            (nameof(DeleteTaxTariffRequest.Reason), new DeleteTaxTariffRequest(id, new string('x', 501), 1)),
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
        string currency = "EUR") =>
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
            currency);

    private static UpdateTaxTariffRequest Update(
        Guid? taxTariffId = null,
        string currency = "EUR",
        long version = 1) =>
        new(
            taxTariffId ?? Guid.NewGuid(),
            null,
            null,
            null,
            null,
            null,
            null,
            currency,
            version);
}

public sealed class TransferValidatorMutationTests
{
    private static readonly DateOnly SupplyMonth = new(2025, 2, 1);
    private static readonly DateOnly StartDay = new(2025, 2, 10);

    [Fact]
    public void Create_accepts_a_valid_request_and_equal_day_boundaries()
    {
        var request = Create(startDay: StartDay, endDay: StartDay);

        new CreateTransferValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void Create_accepts_omitted_optional_dates_and_enums()
    {
        new CreateTransferValidator().Validate(Create()).ShouldBeValid();
    }

    [Fact]
    public void Create_accepts_either_optional_day_without_the_other()
    {
        var requests = new[]
        {
            Create(startDay: StartDay),
            Create(endDay: StartDay),
        };

        foreach (var request in requests)
            new CreateTransferValidator().Validate(request).ShouldBeValid(request.ToString());
    }

    [Fact]
    public void Create_rejects_an_empty_contract_id()
    {
        var request = Create(contractId: Guid.Empty);

        new CreateTransferValidator().Validate(request)
            .ShouldRejectProperty(nameof(CreateTransferRequest.ContractId));
    }

    [Theory]
    [MemberData(nameof(InvalidSupplyMonths))]
    public void Create_rejects_default_or_non_month_start_supply_dates(DateOnly supplyMonth)
    {
        var request = Create(supplyMonth: supplyMonth);

        new CreateTransferValidator().Validate(request)
            .ShouldRejectProperty(nameof(CreateTransferRequest.SupplyMonth));
    }

    public static TheoryData<DateOnly> InvalidSupplyMonths => new()
    {
        default(DateOnly),
        new DateOnly(2025, 2, 2),
    };

    [Fact]
    public void Create_rejects_an_end_day_before_the_start_day()
    {
        var request = Create(startDay: StartDay, endDay: StartDay.AddDays(-1));

        new CreateTransferValidator().Validate(request)
            .ShouldRejectProperty(nameof(CreateTransferRequest.EndDay));
    }

    [Theory]
    [MemberData(nameof(GasPriceMechanisms))]
    public void Create_accepts_every_exact_gas_price_mechanism(string priceMechanism)
    {
        var request = Create(priceMechanism: priceMechanism);

        new CreateTransferValidator().Validate(request).ShouldBeValid();
    }

    [Theory]
    [MemberData(nameof(ReportStatuses))]
    public void Create_accepts_every_exact_report_status(string status)
    {
        var request = Create(status: status);

        new CreateTransferValidator().Validate(request).ShouldBeValid();
    }

    [Theory]
    [InlineData("")]
    [InlineData("fixed")]
    [InlineData("WITHIN DAY MKT")]
    public void Create_rejects_unknown_or_non_exact_price_mechanisms(string priceMechanism)
    {
        var request = Create(priceMechanism: priceMechanism);

        new CreateTransferValidator().Validate(request)
            .ShouldRejectProperty(nameof(CreateTransferRequest.PriceMechanism));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Completed")]
    [InlineData("awaiting")]
    public void Create_rejects_unknown_or_non_exact_statuses(string status)
    {
        var request = Create(status: status);

        new CreateTransferValidator().Validate(request)
            .ShouldRejectProperty(nameof(CreateTransferRequest.Status));
    }

    [Fact]
    public void Update_rejects_an_empty_id()
    {
        var request = EmptyUpdate() with { TransferId = Guid.Empty, TradingArea = "DK1" };

        new UpdateTransferValidator().Validate(request)
            .ShouldRejectProperty(nameof(UpdateTransferRequest.TransferId));
    }

    [Fact]
    public void Update_rejects_an_empty_patch_with_the_documented_message()
    {
        new UpdateTransferValidator().Validate(EmptyUpdate())
            .ShouldRejectRequest("At least one mutable field is required.");
    }

    [Fact]
    public void Update_accepts_each_mutable_field_independently()
    {
        var requests = new[]
        {
            EmptyUpdate() with { TradingArea = string.Empty },
            EmptyUpdate() with { CapacityMw = 0m },
            EmptyUpdate() with { BookedCapacityMw = 0m },
            EmptyUpdate() with { VolumeMwh = 0m },
            EmptyUpdate() with { BalancingEffectMwh = 0m },
            EmptyUpdate() with { PriceMechanism = "FIXED" },
            EmptyUpdate() with { TransportCostEurMwh = 0m },
            EmptyUpdate() with { CapacityCostEurMwh = 0m },
            EmptyUpdate() with { Status = "Awaiting" },
            EmptyUpdate() with { Comments = string.Empty },
        };

        foreach (var request in requests)
            new UpdateTransferValidator().Validate(request).ShouldBeValid(request.ToString());
    }

    [Theory]
    [MemberData(nameof(GasPriceMechanisms))]
    public void Update_accepts_every_exact_gas_price_mechanism(string priceMechanism)
    {
        var request = EmptyUpdate() with { PriceMechanism = priceMechanism };

        new UpdateTransferValidator().Validate(request).ShouldBeValid();
    }

    [Theory]
    [MemberData(nameof(ReportStatuses))]
    public void Update_accepts_every_exact_report_status(string status)
    {
        var request = EmptyUpdate() with { Status = status };

        new UpdateTransferValidator().Validate(request).ShouldBeValid();
    }

    [Theory]
    [InlineData("")]
    [InlineData("ttf")]
    [InlineData("WITHIN DAY MKT")]
    public void Update_rejects_unknown_or_non_exact_price_mechanisms(string priceMechanism)
    {
        var request = EmptyUpdate() with { PriceMechanism = priceMechanism };

        new UpdateTransferValidator().Validate(request)
            .ShouldRejectProperty(nameof(UpdateTransferRequest.PriceMechanism));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Completed")]
    [InlineData("issue")]
    public void Update_rejects_unknown_or_non_exact_statuses(string status)
    {
        var request = EmptyUpdate() with { Status = status };

        new UpdateTransferValidator().Validate(request)
            .ShouldRejectProperty(nameof(UpdateTransferRequest.Status));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Update_rejects_non_positive_versions(long version)
    {
        var request = EmptyUpdate() with { TradingArea = "DK1", Version = version };

        new UpdateTransferValidator().Validate(request)
            .ShouldRejectProperty(nameof(UpdateTransferRequest.Version));
    }

    [Fact]
    public void Cancel_accepts_exact_reason_and_version_boundaries()
    {
        var request = new CancelTransferRequest(Guid.NewGuid(), new string('x', 500), 1);

        new CancelTransferValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void Cancel_rejects_each_required_or_bounded_value_outside_its_contract()
    {
        var id = Guid.NewGuid();
        var cases = new[]
        {
            (nameof(CancelTransferRequest.TransferId), new CancelTransferRequest(Guid.Empty, "reason", 1)),
            (nameof(CancelTransferRequest.Reason), new CancelTransferRequest(id, null!, 1)),
            (nameof(CancelTransferRequest.Reason), new CancelTransferRequest(id, string.Empty, 1)),
            (nameof(CancelTransferRequest.Reason), new CancelTransferRequest(id, "   ", 1)),
            (nameof(CancelTransferRequest.Reason), new CancelTransferRequest(id, new string('x', 501), 1)),
            (nameof(CancelTransferRequest.Version), new CancelTransferRequest(id, "reason", 0)),
            (nameof(CancelTransferRequest.Version), new CancelTransferRequest(id, "reason", -1)),
        };

        foreach (var (propertyName, request) in cases)
            new CancelTransferValidator().Validate(request).ShouldRejectProperty(propertyName);
    }

    public static TheoryData<string> GasPriceMechanisms => new()
    {
        "FIXED",
        "VARIABLE",
        "EGSI ETF",
        "TTF",
        "WITHIN-DAY MKT",
        "BGO",
        "PGO",
        "THE",
    };

    public static TheoryData<string> ReportStatuses => new()
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
        string? status = null) =>
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
            null);

    private static UpdateTransferRequest EmptyUpdate() => new(
        Guid.NewGuid(),
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        1);
}

public sealed class PhysicalDeliveryValidatorMutationBoundaryTests
{
    private static readonly DateOnly SupplyMonth = new(2025, 2, 1);
    private static readonly DateOnly StartDay = new(2025, 2, 10);

    [Fact]
    public void Create_accepts_exact_instance_date_and_numeric_boundaries()
    {
        var request = Create(
            instanceId: new string('x', 120),
            startDay: StartDay,
            endDay: StartDay,
            nominated: 0m,
            realised: 0m);

        new CreatePhysicalDeliveryValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void Create_accepts_an_omitted_instance_optional_dates_and_optional_numeric_values()
    {
        var request = Create(instanceId: null, nominated: null, realised: null);

        new CreatePhysicalDeliveryValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void Create_accepts_either_optional_day_without_the_other()
    {
        var requests = new[]
        {
            Create(startDay: StartDay),
            Create(endDay: StartDay),
        };

        foreach (var request in requests)
            new CreatePhysicalDeliveryValidator().Validate(request).ShouldBeValid(request.ToString());
    }

    [Fact]
    public void Create_rejects_an_empty_contract_id()
    {
        var request = Create(contractId: Guid.Empty);

        new CreatePhysicalDeliveryValidator().Validate(request)
            .ShouldRejectProperty(nameof(CreatePhysicalDeliveryRequest.ContractId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_an_empty_supplied_instance_id(string instanceId)
    {
        var request = Create(instanceId: instanceId);

        new CreatePhysicalDeliveryValidator().Validate(request)
            .ShouldRejectProperty(nameof(CreatePhysicalDeliveryRequest.ContractInstanceId));
    }

    [Fact]
    public void Create_rejects_an_instance_id_above_120_characters()
    {
        var request = Create(instanceId: new string('x', 121));

        new CreatePhysicalDeliveryValidator().Validate(request)
            .ShouldRejectProperty(nameof(CreatePhysicalDeliveryRequest.ContractInstanceId));
    }

    [Theory]
    [InlineData("Sourcing")]
    [InlineData("Sales")]
    [InlineData("Intercompany")]
    public void Create_accepts_every_exact_book_type(string bookType)
    {
        new CreatePhysicalDeliveryValidator().Validate(Create(bookType: bookType)).ShouldBeValid();
    }

    [Theory]
    [InlineData("")]
    [InlineData("sales")]
    [InlineData("Internal")]
    public void Create_rejects_unknown_or_non_exact_book_types(string bookType)
    {
        new CreatePhysicalDeliveryValidator().Validate(Create(bookType: bookType))
            .ShouldRejectProperty(nameof(CreatePhysicalDeliveryRequest.BookType));
    }

    [Theory]
    [MemberData(nameof(InvalidSupplyMonths))]
    public void Create_rejects_default_or_non_month_start_supply_dates(
        DateOnly supplyMonth,
        string expectedPropertyName)
    {
        new CreatePhysicalDeliveryValidator().Validate(Create(supplyMonth: supplyMonth))
            .ShouldRejectProperty(expectedPropertyName);
    }

    public static TheoryData<DateOnly, string> InvalidSupplyMonths => new()
    {
        { default(DateOnly), nameof(CreatePhysicalDeliveryRequest.SupplyMonth) },
        { new DateOnly(2025, 2, 2), "SupplyMonth.Day" },
    };

    [Fact]
    public void Create_rejects_an_end_day_before_the_start_day()
    {
        var request = Create(startDay: StartDay, endDay: StartDay.AddDays(-1));

        new CreatePhysicalDeliveryValidator().Validate(request)
            .ShouldRejectProperty(nameof(CreatePhysicalDeliveryRequest.EndDay));
    }

    [Fact]
    public void Create_rejects_each_bounded_numeric_value_below_zero()
    {
        var cases = new[]
        {
            (nameof(CreatePhysicalDeliveryRequest.VolumeNominatedMwh), Create(nominated: -0.01m)),
            (nameof(CreatePhysicalDeliveryRequest.VolumeRealisedMwh), Create(realised: -0.01m)),
        };

        foreach (var (propertyName, request) in cases)
            new CreatePhysicalDeliveryValidator().Validate(request).ShouldRejectProperty(propertyName);
    }

    [Fact]
    public void Update_rejects_an_empty_id()
    {
        var request = new UpdatePhysicalDeliveryRequest(Guid.Empty, 0m, null, 1);

        new UpdatePhysicalDeliveryValidator().Validate(request)
            .ShouldRejectProperty(nameof(UpdatePhysicalDeliveryRequest.DeliveryId));
    }

    [Fact]
    public void Update_rejects_an_empty_patch_with_the_documented_message()
    {
        var request = new UpdatePhysicalDeliveryRequest(Guid.NewGuid(), null, null, 1);

        new UpdatePhysicalDeliveryValidator().Validate(request)
            .ShouldRejectRequest("At least one mutable field is required.");
    }

    [Fact]
    public void Update_accepts_each_mutable_field_independently_at_its_boundary()
    {
        var requests = new[]
        {
            new UpdatePhysicalDeliveryRequest(Guid.NewGuid(), 0m, null, 1),
            new UpdatePhysicalDeliveryRequest(Guid.NewGuid(), null, "Awaiting", 1),
        };

        foreach (var request in requests)
            new UpdatePhysicalDeliveryValidator().Validate(request).ShouldBeValid(request.ToString());
    }

    [Fact]
    public void Update_rejects_realised_volume_below_zero()
    {
        var request = new UpdatePhysicalDeliveryRequest(Guid.NewGuid(), -0.01m, null, 1);

        new UpdatePhysicalDeliveryValidator().Validate(request)
            .ShouldRejectProperty(nameof(UpdatePhysicalDeliveryRequest.VolumeRealisedMwh));
    }

    [Theory]
    [MemberData(nameof(ReportStatuses))]
    public void Update_accepts_every_exact_report_status(string status)
    {
        var request = new UpdatePhysicalDeliveryRequest(Guid.NewGuid(), null, status, 1);

        new UpdatePhysicalDeliveryValidator().Validate(request).ShouldBeValid();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Completed")]
    [InlineData("awaiting")]
    public void Update_rejects_unknown_or_non_exact_report_statuses(string status)
    {
        var request = new UpdatePhysicalDeliveryRequest(Guid.NewGuid(), null, status, 1);

        new UpdatePhysicalDeliveryValidator().Validate(request)
            .ShouldRejectProperty(nameof(UpdatePhysicalDeliveryRequest.Status));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Update_rejects_non_positive_versions(long version)
    {
        var request = new UpdatePhysicalDeliveryRequest(Guid.NewGuid(), 0m, null, version);

        new UpdatePhysicalDeliveryValidator().Validate(request)
            .ShouldRejectProperty(nameof(UpdatePhysicalDeliveryRequest.Version));
    }

    [Fact]
    public void Delete_accepts_exact_reason_and_version_boundaries()
    {
        var request = new DeletePhysicalDeliveryRequest(Guid.NewGuid(), new string('x', 500), 1);

        new DeletePhysicalDeliveryValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void Delete_rejects_each_required_or_bounded_value_outside_its_contract()
    {
        var id = Guid.NewGuid();
        var cases = new[]
        {
            (nameof(DeletePhysicalDeliveryRequest.DeliveryId), new DeletePhysicalDeliveryRequest(Guid.Empty, "reason", 1)),
            (nameof(DeletePhysicalDeliveryRequest.Reason), new DeletePhysicalDeliveryRequest(id, null!, 1)),
            (nameof(DeletePhysicalDeliveryRequest.Reason), new DeletePhysicalDeliveryRequest(id, string.Empty, 1)),
            (nameof(DeletePhysicalDeliveryRequest.Reason), new DeletePhysicalDeliveryRequest(id, "   ", 1)),
            (nameof(DeletePhysicalDeliveryRequest.Reason), new DeletePhysicalDeliveryRequest(id, new string('x', 501), 1)),
            (nameof(DeletePhysicalDeliveryRequest.Version), new DeletePhysicalDeliveryRequest(id, "reason", 0)),
            (nameof(DeletePhysicalDeliveryRequest.Version), new DeletePhysicalDeliveryRequest(id, "reason", -1)),
        };

        foreach (var (propertyName, request) in cases)
            new DeletePhysicalDeliveryValidator().Validate(request).ShouldRejectProperty(propertyName);
    }

    public static TheoryData<string> ReportStatuses => new()
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
        decimal? realised = 1m) =>
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
            endDay);
}

internal static class ValidatorMutationAssertions
{
    public static void ShouldBeValid(this ValidationResult result, string because = "the boundary is valid")
    {
        result.IsValid.Should().BeTrue(
            "because {0}; validation errors were {1}",
            because,
            string.Join(" | ", result.Errors.Select(error => error.ErrorMessage)));
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
        result.Errors.Should().ContainSingle(error =>
            error.PropertyName == string.Empty && error.ErrorMessage == exactMessage);
    }
}
