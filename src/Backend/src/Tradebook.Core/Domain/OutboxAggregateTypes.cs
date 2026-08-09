namespace Tradebook.Core.Domain;

public static class OutboxAggregateTypes
{
    public const string PhysicalDelivery = nameof(PhysicalDelivery);
    public const string Contract = nameof(Contract);
    public const string CapacityBooking = nameof(CapacityBooking);
    public const string Transfer = nameof(Transfer);
    public const string BioticketDelivery = nameof(BioticketDelivery);
    public const string GooCertificateTransaction = nameof(GooCertificateTransaction);
    public const string MarketPrice = nameof(MarketPrice);
    public const string TaxTariff = nameof(TaxTariff);
    public const string Hedge = nameof(Hedge);
    public const string WorkspaceDashboard = nameof(WorkspaceDashboard);

    public static IReadOnlySet<string> All { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            PhysicalDelivery,
            Contract,
            CapacityBooking,
            Transfer,
            BioticketDelivery,
            GooCertificateTransaction,
            MarketPrice,
            TaxTariff,
            Hedge,
            WorkspaceDashboard,
        };

    public static bool IsKnown(string aggregateType) => All.Contains(aggregateType);
}
