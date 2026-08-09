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

    private static readonly Dictionary<Type, Func<object>> Factories = new()
    {
        [typeof(ContractId)] = static () =>
            new GuidFormatter<ContractId>(value => value.Value, ContractId.From),
        [typeof(DeliveryId)] = static () =>
            new GuidFormatter<DeliveryId>(value => value.Value, DeliveryId.From),
        [typeof(CapacityBookingId)] = static () =>
            new GuidFormatter<CapacityBookingId>(value => value.Value, CapacityBookingId.From),
        [typeof(TransferId)] = static () =>
            new GuidFormatter<TransferId>(value => value.Value, TransferId.From),
        [typeof(BioticketDeliveryId)] = static () =>
            new GuidFormatter<BioticketDeliveryId>(value => value.Value, BioticketDeliveryId.From),
        [typeof(CounterpartyId)] = static () =>
            new GuidFormatter<CounterpartyId>(value => value.Value, CounterpartyId.From),
        [typeof(CompanyId)] = static () =>
            new GuidFormatter<CompanyId>(value => value.Value, CompanyId.From),
        [typeof(TradingPointId)] = static () =>
            new GuidFormatter<TradingPointId>(value => value.Value, TradingPointId.From),
        [typeof(TaxTariffId)] = static () =>
            new GuidFormatter<TaxTariffId>(value => value.Value, TaxTariffId.From),
        [typeof(HedgeId)] = static () =>
            new GuidFormatter<HedgeId>(value => value.Value, HedgeId.From),
        [typeof(MarketPriceId)] = static () =>
            new GuidFormatter<MarketPriceId>(value => value.Value, MarketPriceId.From),
        [typeof(CapacityPriceIndexId)] = static () =>
            new GuidFormatter<CapacityPriceIndexId>(
                value => value.Value,
                CapacityPriceIndexId.From
            ),
        [typeof(GooCertificateTransactionId)] = static () =>
            new GuidFormatter<GooCertificateTransactionId>(
                value => value.Value,
                GooCertificateTransactionId.From
            ),
        [typeof(InvoiceLineItemId)] = static () =>
            new GuidFormatter<InvoiceLineItemId>(value => value.Value, InvoiceLineItemId.From),
        [typeof(UserId)] = static () =>
            new GuidFormatter<UserId>(value => value.Value, UserId.From),
        [typeof(DashboardId)] = static () =>
            new GuidFormatter<DashboardId>(value => value.Value, DashboardId.From),
        [typeof(DomainEventId)] = static () =>
            new GuidFormatter<DomainEventId>(value => value.Value, DomainEventId.From),
        [typeof(AuditLogId)] = static () =>
            new GuidFormatter<AuditLogId>(value => value.Value, AuditLogId.From),
        [typeof(Price)] = static () =>
            new DecimalFormatter<Price>(value => value.Value, Price.From),
        [typeof(Quantity)] = static () =>
            new DecimalFormatter<Quantity>(value => value.Value, Quantity.From),
        [typeof(Amount)] = static () =>
            new DecimalFormatter<Amount>(value => value.Value, Amount.From),
    };

    private static class FormatterCache<T>
    {
        internal static readonly IMessagePackFormatter<T>? Formatter = Create();

        private static IMessagePackFormatter<T>? Create() =>
            Factories.TryGetValue(typeof(T), out var factory)
                ? (IMessagePackFormatter<T>?)factory()
                : null;
    }

    private sealed class GuidFormatter<T>(Func<T, Guid> unwrap, Func<Guid, T> factory)
        : IMessagePackFormatter<T>
    {
        public void Serialize(
            ref MessagePackWriter writer,
            T value,
            MessagePackSerializerOptions options
        ) =>
            options
                .Resolver.GetFormatterWithVerify<Guid>()
                .Serialize(ref writer, unwrap(value), options);

        public T Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) =>
            factory(
                options.Resolver.GetFormatterWithVerify<Guid>().Deserialize(ref reader, options)
            );
    }

    private sealed class DecimalFormatter<T>(Func<T, decimal> unwrap, Func<decimal, T> factory)
        : IMessagePackFormatter<T>
    {
        public void Serialize(
            ref MessagePackWriter writer,
            T value,
            MessagePackSerializerOptions options
        ) =>
            options
                .Resolver.GetFormatterWithVerify<decimal>()
                .Serialize(ref writer, unwrap(value), options);

        public T Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) =>
            factory(
                options.Resolver.GetFormatterWithVerify<decimal>().Deserialize(ref reader, options)
            );
    }
}
