using MessagePack;
using MessagePack.Formatters;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using DomainEventId = Tradebook.Core.Domain.ValueObjects.Ids.EventId;

namespace Tradebook.Api.RealTime;

public sealed class VogenMessagePackResolver : IFormatterResolver
{
    public static VogenMessagePackResolver Instance { get; } = new();

    private VogenMessagePackResolver() { }

    public IMessagePackFormatter<T>? GetFormatter<T>() => FormatterCache<T>.Formatter;

    private static class FormatterCache<T>
    {
        internal static readonly IMessagePackFormatter<T>? Formatter = Create();

        private static IMessagePackFormatter<T>? Create()
        {
            object? formatter = typeof(T) switch
            {
                var type when type == typeof(ContractId) => new GuidFormatter<ContractId>(value => value.Value, ContractId.From),
                var type when type == typeof(DeliveryId) => new GuidFormatter<DeliveryId>(value => value.Value, DeliveryId.From),
                var type when type == typeof(CapacityBookingId) => new GuidFormatter<CapacityBookingId>(value => value.Value, CapacityBookingId.From),
                var type when type == typeof(TransferId) => new GuidFormatter<TransferId>(value => value.Value, TransferId.From),
                var type when type == typeof(BioticketDeliveryId) => new GuidFormatter<BioticketDeliveryId>(value => value.Value, BioticketDeliveryId.From),
                var type when type == typeof(CounterpartyId) => new GuidFormatter<CounterpartyId>(value => value.Value, CounterpartyId.From),
                var type when type == typeof(CompanyId) => new GuidFormatter<CompanyId>(value => value.Value, CompanyId.From),
                var type when type == typeof(TradingPointId) => new GuidFormatter<TradingPointId>(value => value.Value, TradingPointId.From),
                var type when type == typeof(TaxTariffId) => new GuidFormatter<TaxTariffId>(value => value.Value, TaxTariffId.From),
                var type when type == typeof(HedgeId) => new GuidFormatter<HedgeId>(value => value.Value, HedgeId.From),
                var type when type == typeof(MarketPriceId) => new GuidFormatter<MarketPriceId>(value => value.Value, MarketPriceId.From),
                var type when type == typeof(CapacityPriceIndexId) => new GuidFormatter<CapacityPriceIndexId>(value => value.Value, CapacityPriceIndexId.From),
                var type when type == typeof(GooCertificateTransactionId) => new GuidFormatter<GooCertificateTransactionId>(value => value.Value, GooCertificateTransactionId.From),
                var type when type == typeof(InvoiceLineItemId) => new GuidFormatter<InvoiceLineItemId>(value => value.Value, InvoiceLineItemId.From),
                var type when type == typeof(UserId) => new GuidFormatter<UserId>(value => value.Value, UserId.From),
                var type when type == typeof(DashboardId) => new GuidFormatter<DashboardId>(value => value.Value, DashboardId.From),
                var type when type == typeof(DomainEventId) => new GuidFormatter<DomainEventId>(value => value.Value, DomainEventId.From),
                var type when type == typeof(AuditLogId) => new GuidFormatter<AuditLogId>(value => value.Value, AuditLogId.From),
                var type when type == typeof(Price) => new DecimalFormatter<Price>(value => value.Value, Price.From),
                var type when type == typeof(Quantity) => new DecimalFormatter<Quantity>(value => value.Value, Quantity.From),
                var type when type == typeof(Amount) => new DecimalFormatter<Amount>(value => value.Value, Amount.From),
                _ => null
            };

            return (IMessagePackFormatter<T>?)formatter;
        }
    }

    private sealed class GuidFormatter<T>(Func<T, Guid> unwrap, Func<Guid, T> factory) : IMessagePackFormatter<T>
    {
        public void Serialize(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options) =>
            options.Resolver.GetFormatterWithVerify<Guid>().Serialize(ref writer, unwrap(value), options);

        public T Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) =>
            factory(options.Resolver.GetFormatterWithVerify<Guid>().Deserialize(ref reader, options));
    }

    private sealed class DecimalFormatter<T>(Func<T, decimal> unwrap, Func<decimal, T> factory) : IMessagePackFormatter<T>
    {
        public void Serialize(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options) =>
            options.Resolver.GetFormatterWithVerify<decimal>().Serialize(ref writer, unwrap(value), options);

        public T Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) =>
            factory(options.Resolver.GetFormatterWithVerify<decimal>().Deserialize(ref reader, options));
    }
}
