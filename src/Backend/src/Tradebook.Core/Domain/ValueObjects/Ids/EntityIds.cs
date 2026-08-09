using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Ids;

[ValueObject<Guid>]
public readonly partial struct ContractId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(ContractId));

    public static ContractId New() => From(Guid.CreateVersion7());
}

[ValueObject<Guid>]
public readonly partial struct DeliveryId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(DeliveryId));

    public static DeliveryId New() => From(Guid.CreateVersion7());
}

[ValueObject<Guid>]
public readonly partial struct CapacityBookingId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(CapacityBookingId));

    public static CapacityBookingId New() => From(Guid.CreateVersion7());
}

[ValueObject<Guid>]
public readonly partial struct TransferId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(TransferId));

    public static TransferId New() => From(Guid.CreateVersion7());
}

[ValueObject<Guid>]
public readonly partial struct BioticketDeliveryId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(BioticketDeliveryId));

    public static BioticketDeliveryId New() => From(Guid.CreateVersion7());
}

[ValueObject<Guid>]
public readonly partial struct CounterpartyId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(CounterpartyId));

    public static CounterpartyId New() => From(Guid.CreateVersion7());
}

[ValueObject<Guid>]
public readonly partial struct CompanyId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(CompanyId));

    public static CompanyId New() => From(Guid.CreateVersion7());
}

[ValueObject<Guid>]
public readonly partial struct TradingPointId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(TradingPointId));

    public static TradingPointId New() => From(Guid.CreateVersion7());
}

[ValueObject<Guid>]
public readonly partial struct TaxTariffId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(TaxTariffId));

    public static TaxTariffId New() => From(Guid.CreateVersion7());
}

[ValueObject<Guid>]
public readonly partial struct HedgeId
{
    private static Validation Validate(Guid value) => IdValidation.Validate(value, nameof(HedgeId));

    public static HedgeId New() => From(Guid.CreateVersion7());
}

[ValueObject<Guid>]
public readonly partial struct MarketPriceId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(MarketPriceId));

    public static MarketPriceId New() => From(Guid.CreateVersion7());
}

[ValueObject<Guid>]
public readonly partial struct CapacityPriceIndexId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(CapacityPriceIndexId));

    public static CapacityPriceIndexId New() => From(Guid.CreateVersion7());
}

[ValueObject<Guid>]
public readonly partial struct GooCertificateTransactionId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(GooCertificateTransactionId));

    public static GooCertificateTransactionId New() => From(Guid.CreateVersion7());
}

[ValueObject<Guid>]
public readonly partial struct InvoiceLineItemId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(InvoiceLineItemId));

    public static InvoiceLineItemId New() => From(Guid.CreateVersion7());
}

[ValueObject<Guid>]
public readonly partial struct UserId
{
    private static Validation Validate(Guid value) => IdValidation.Validate(value, nameof(UserId));

    public static UserId New() => From(Guid.CreateVersion7());
}

[ValueObject<Guid>]
public readonly partial struct DashboardId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(DashboardId));

    public static DashboardId New() => From(Guid.CreateVersion7());
}

[ValueObject<Guid>]
public readonly partial struct EventId
{
    private static Validation Validate(Guid value) => IdValidation.Validate(value, nameof(EventId));

    public static EventId New() => From(Guid.CreateVersion7());
}

[ValueObject<Guid>]
public readonly partial struct AuditLogId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(AuditLogId));

    public static AuditLogId New() => From(Guid.CreateVersion7());
}

internal static class IdValidation
{
    internal static Validation Validate(Guid value, string typeName) =>
        value == Guid.Empty ? Validation.Invalid($"{typeName} must not be empty.") : Validation.Ok;
}
