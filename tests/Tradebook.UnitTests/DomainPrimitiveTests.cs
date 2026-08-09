using System.Text.Json;
using MessagePack;
using MessagePack.Resolvers;
using Tradebook.Api;
using Tradebook.Api.RealTime;
using Tradebook.Core.Domain;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using Tradebook.Infrastructure.Data;

namespace Tradebook.UnitTests;

public sealed class DomainPrimitiveTests
{
    [Fact]
    public void PriceRejectsNegativeValuesAndScaleAboveFour()
    {
        Assert.Throws<TradebookDomainException>(() => Price.From(-0.0001m));
        Assert.Throws<TradebookDomainException>(() => Price.From(1.00001m));
        Assert.Equal(1.2345m, Price.From(1.2345m).Value);
    }

    [Fact]
    public void QuantityRejectsScaleAboveEightButAllowsNegativeCorrections()
    {
        Assert.Throws<TradebookDomainException>(() => Quantity.From(1.000000001m));
        Assert.Equal(-1.00000001m, Quantity.From(-1.00000001m).Value);
    }

    [Fact]
    public void AmountRejectsScaleAboveFour()
    {
        Assert.Throws<TradebookDomainException>(() => Amount.From(0.00001m));
        Assert.Equal(-12.3456m, Amount.From(-12.3456m).Value);
    }

    [Fact]
    public void IdentifierTypesRejectEmptyAndAreNotAssignmentCompatible()
    {
        Assert.Throws<TradebookDomainException>(() => ContractId.From(Guid.Empty));
        Assert.NotEqual(typeof(ContractId), typeof(CounterpartyId));
        Assert.NotEqual(typeof(DeliveryId), typeof(TransferId));
    }

    public static TheoryData<object, Type> ValueObjects =>
        new()
        {
            { ContractId.New(), typeof(ContractId) },
            { DeliveryId.New(), typeof(DeliveryId) },
            { CapacityBookingId.New(), typeof(CapacityBookingId) },
            { TransferId.New(), typeof(TransferId) },
            { BioticketDeliveryId.New(), typeof(BioticketDeliveryId) },
            { CounterpartyId.New(), typeof(CounterpartyId) },
            { CompanyId.New(), typeof(CompanyId) },
            { TradingPointId.New(), typeof(TradingPointId) },
            { TaxTariffId.New(), typeof(TaxTariffId) },
            { HedgeId.New(), typeof(HedgeId) },
            { MarketPriceId.New(), typeof(MarketPriceId) },
            { CapacityPriceIndexId.New(), typeof(CapacityPriceIndexId) },
            { GooCertificateTransactionId.New(), typeof(GooCertificateTransactionId) },
            { InvoiceLineItemId.New(), typeof(InvoiceLineItemId) },
            { UserId.New(), typeof(UserId) },
            { DashboardId.New(), typeof(DashboardId) },
            { EventId.New(), typeof(EventId) },
            { AuditLogId.New(), typeof(AuditLogId) },
            { Price.From(12.3456m), typeof(Price) },
            { Quantity.From(12.34567890m), typeof(Quantity) },
            { Amount.From(-12.3456m), typeof(Amount) },
        };

    [Theory]
    [MemberData(nameof(ValueObjects))]
    public void EveryValueObjectRoundTripsAsItsPrimitiveWithSourceGeneratedJson(
        object value,
        Type type
    )
    {
        var json = JsonSerializer.Serialize(value, type, AppJsonSerializerContext.Default.Options);
        var deserialized = JsonSerializer.Deserialize(
            json,
            type,
            AppJsonSerializerContext.Default.Options
        );

        Assert.Equal(value, deserialized);
        Assert.DoesNotContain("value", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EveryValueObjectHasARegisteredDapperHandler()
    {
        VogenTypeHandlers.RegisterAll();

        foreach (var row in ValueObjects)
        {
            var type = row.Data.Item2;
            Assert.True(
                Dapper.SqlMapper.HasTypeHandler(type),
                $"No Dapper handler is registered for {type.Name}."
            );
        }
    }

    [Fact]
    public void MessagePackResolverRoundTripsIdentifierMoneyAndQuantityPrimitives()
    {
        var options = MessagePackSerializerOptions.Standard.WithResolver(
            CompositeResolver.Create(VogenMessagePackResolver.Instance, StandardResolver.Instance)
        );

        AssertRoundTrip(ContractId.New(), options);
        AssertRoundTrip(Price.From(12.3456m), options);
        AssertRoundTrip(Quantity.From(-12.34567890m), options);
        AssertRoundTrip(Amount.From(-12.3456m), options);
    }

    private static void AssertRoundTrip<T>(T value, MessagePackSerializerOptions options) =>
        Assert.Equal(
            value,
            MessagePackSerializer.Deserialize<T>(
                MessagePackSerializer.Serialize(value, options),
                options
            )
        );
}
