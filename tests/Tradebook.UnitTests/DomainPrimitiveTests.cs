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
    public void Price_rejects_negative_values_and_scale_above_four()
    {
        Assert.Throws<TradebookDomainException>(() => Price.From(-0.0001m));
        Assert.Throws<TradebookDomainException>(() => Price.From(1.00001m));
        Assert.Equal(1.2345m, Price.From(1.2345m).Value);
    }

    [Fact]
    public void Quantity_rejects_scale_above_eight_but_allows_negative_corrections()
    {
        Assert.Throws<TradebookDomainException>(() => Quantity.From(1.000000001m));
        Assert.Equal(-1.00000001m, Quantity.From(-1.00000001m).Value);
    }

    [Fact]
    public void Amount_rejects_scale_above_four()
    {
        Assert.Throws<TradebookDomainException>(() => Amount.From(0.00001m));
        Assert.Equal(-12.3456m, Amount.From(-12.3456m).Value);
    }

    [Fact]
    public void Identifier_types_reject_empty_and_are_not_assignment_compatible()
    {
        Assert.Throws<TradebookDomainException>(() => ContractId.From(Guid.Empty));
        Assert.NotEqual(typeof(ContractId), typeof(CounterpartyId));
        Assert.NotEqual(typeof(DeliveryId), typeof(TransferId));
    }

    public static TheoryData<object, Type> ValueObjects => new()
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
        { Amount.From(-12.3456m), typeof(Amount) }
    };

    [Theory]
    [MemberData(nameof(ValueObjects))]
    public void Every_value_object_round_trips_as_its_primitive_with_source_generated_json(object value, Type type)
    {
        var json = JsonSerializer.Serialize(value, type, AppJsonSerializerContext.Default.Options);
        var deserialized = JsonSerializer.Deserialize(json, type, AppJsonSerializerContext.Default.Options);

        Assert.Equal(value, deserialized);
        Assert.DoesNotContain("value", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Every_value_object_has_a_registered_dapper_handler()
    {
        VogenTypeHandlers.RegisterAll();

        foreach (var row in ValueObjects)
        {
            var type = (Type)row[1];
            Assert.True(Dapper.SqlMapper.HasTypeHandler(type), $"No Dapper handler is registered for {type.Name}.");
        }
    }

    [Fact]
    public void MessagePack_resolver_round_trips_identifier_money_and_quantity_primitives()
    {
        var options = MessagePackSerializerOptions.Standard.WithResolver(
            CompositeResolver.Create(VogenMessagePackResolver.Instance, StandardResolver.Instance));

        AssertRoundTrip(ContractId.New(), options);
        AssertRoundTrip(Price.From(12.3456m), options);
        AssertRoundTrip(Quantity.From(-12.34567890m), options);
        AssertRoundTrip(Amount.From(-12.3456m), options);
    }

    private static void AssertRoundTrip<T>(T value, MessagePackSerializerOptions options) =>
        Assert.Equal(value, MessagePackSerializer.Deserialize<T>(MessagePackSerializer.Serialize(value, options), options));
}
