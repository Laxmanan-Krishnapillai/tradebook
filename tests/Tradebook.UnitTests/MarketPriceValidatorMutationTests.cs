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

public sealed class MarketPriceValidatorMutationTests
{
    private static readonly DateOnly PriceDate = new(2025, 2, 14);

    [Fact]
    public void UpsertAcceptsEachMarketOrFxValueAsTheOnlySuppliedValue()
    {
        var requests = new[]
        {
            EmptyUpsert() with
            {
                TtfEurMwh = 1m,
            },
            EmptyUpsert() with
            {
                EgsiEtfEurMwh = 1m,
            },
            EmptyUpsert() with
            {
                TheEurMwh = 1m,
            },
            EmptyUpsert() with
            {
                BgoEurMwh = 1m,
            },
            EmptyUpsert() with
            {
                PgoEurMwh = 1m,
            },
            EmptyUpsert() with
            {
                EuaEurMwh = 1m,
            },
            EmptyUpsert() with
            {
                WithinDayMktEurMwh = 1m,
            },
            EmptyUpsert() with
            {
                EurSek = 1m,
            },
            EmptyUpsert() with
            {
                EurChf = 1m,
            },
            EmptyUpsert() with
            {
                EurGbp = 1m,
            },
            EmptyUpsert() with
            {
                EurUsd = 1m,
            },
            EmptyUpsert() with
            {
                EurDkk = 1m,
            },
        };

        foreach (var request in requests)
            new UpsertMarketPriceValidator().Validate(request).ShouldBeValid(request.ToString());
    }

    [Fact]
    public void UpsertRejectsARequestWithoutAnyMarketOrFxValue()
    {
        new UpsertMarketPriceValidator()
            .Validate(EmptyUpsert())
            .ShouldRejectRequest("At least one market or FX value is required.");
    }

    [Fact]
    public void UpsertRejectsTheDefaultPriceDate()
    {
        var request = EmptyUpsert() with { PriceDate = default, TtfEurMwh = 1m };

        new UpsertMarketPriceValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(UpsertMarketPriceRequest.PriceDate));
    }

    [Fact]
    public void UpsertAcceptsZeroAsTheVersionLowerBoundary()
    {
        var request = EmptyUpsert() with { TtfEurMwh = 1m, Version = 0 };

        new UpsertMarketPriceValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void UpsertRejectsAVersionBelowZero()
    {
        var request = EmptyUpsert() with { TtfEurMwh = 1m, Version = -1 };

        new UpsertMarketPriceValidator()
            .Validate(request)
            .ShouldRejectProperty(nameof(UpsertMarketPriceRequest.Version));
    }

    [Fact]
    public void UpsertAcceptsNegativeEnergyMarketPrices()
    {
        var request = EmptyUpsert() with { TtfEurMwh = -0.01m };

        new UpsertMarketPriceValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void UpsertAcceptsEachFxRateAboveZero()
    {
        var requests = FxRequests(0.01m);

        foreach (var request in requests)
            new UpsertMarketPriceValidator().Validate(request).ShouldBeValid(request.ToString());
    }

    [Fact]
    public void UpsertRejectsEachFxRateAtZero()
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
    public void UpsertRejectsEachFxRateBelowZero()
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
    public void DeleteAcceptsExactReasonAndVersionBoundaries()
    {
        var request = new DeleteMarketPriceRequest(PriceDate, new string('x', 500), 1);

        new DeleteMarketPriceValidator().Validate(request).ShouldBeValid();
    }

    [Fact]
    public void DeleteRejectsEachRequiredOrBoundedValueOutsideItsContract()
    {
        var cases = new[]
        {
            (
                nameof(DeleteMarketPriceRequest.PriceDate),
                new DeleteMarketPriceRequest(default, "reason", 1)
            ),
            (
                nameof(DeleteMarketPriceRequest.Reason),
                new DeleteMarketPriceRequest(PriceDate, null!, 1)
            ),
            (
                nameof(DeleteMarketPriceRequest.Reason),
                new DeleteMarketPriceRequest(PriceDate, string.Empty, 1)
            ),
            (
                nameof(DeleteMarketPriceRequest.Reason),
                new DeleteMarketPriceRequest(PriceDate, "   ", 1)
            ),
            (
                nameof(DeleteMarketPriceRequest.Reason),
                new DeleteMarketPriceRequest(PriceDate, new string('x', 501), 1)
            ),
            (
                nameof(DeleteMarketPriceRequest.Version),
                new DeleteMarketPriceRequest(PriceDate, "reason", 0)
            ),
            (
                nameof(DeleteMarketPriceRequest.Version),
                new DeleteMarketPriceRequest(PriceDate, "reason", -1)
            ),
        };

        foreach (var (propertyName, request) in cases)
            new DeleteMarketPriceValidator().Validate(request).ShouldRejectProperty(propertyName);
    }

    private static UpsertMarketPriceRequest EmptyUpsert() =>
        new(PriceDate, null, null, null, null, null, null, null, null, null, null, null, null, 0);

    private static UpsertMarketPriceRequest[] FxRequests(decimal value) =>
        [
            EmptyUpsert() with
            {
                EurSek = value,
            },
            EmptyUpsert() with
            {
                EurChf = value,
            },
            EmptyUpsert() with
            {
                EurGbp = value,
            },
            EmptyUpsert() with
            {
                EurUsd = value,
            },
            EmptyUpsert() with
            {
                EurDkk = value,
            },
        ];
}
