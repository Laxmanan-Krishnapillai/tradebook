using Bogus;

namespace Tradebook.Infrastructure.Development;

#pragma warning disable MA0048 // Compact seed-only row records intentionally live beside the builder.
#pragma warning disable MA0051 // Declarative seed rows are clearer when kept as one domain fixture.
#pragma warning disable MA0006 // Fixed seed constants use ordinal equality by construction.
#pragma warning disable S3358 // Nested conditional values mirror the small domain matrices below.
#pragma warning disable CA1859 // IReadOnlyList documents that fixture inputs are not mutated.

internal sealed record DevelopmentSeedData(
    IReadOnlyList<SeedCompany> Companies,
    IReadOnlyList<SeedCounterparty> Counterparties,
    IReadOnlyList<SeedTradingPoint> TradingPoints,
    IReadOnlyList<SeedContract> Contracts,
    IReadOnlyList<SeedDelivery> Deliveries,
    IReadOnlyList<SeedCapacityBooking> CapacityBookings,
    IReadOnlyList<SeedTransfer> Transfers,
    IReadOnlyList<SeedBioticket> Biotickets,
    IReadOnlyList<SeedTaxTariff> TaxTariffs,
    IReadOnlyList<SeedHedge> Hedges,
    IReadOnlyList<SeedMarketPrice> MarketPrices,
    IReadOnlyList<SeedCertificateTransaction> CertificateTransactions
)
{
    private static readonly string[] Statuses =
    [
        "Completed - Payment Received/Sent",
        "In Progress - Invoice Received/Sent",
        "Pending - No Invoice",
        "Awaiting",
        "Issue",
    ];

    private static readonly string[] Segments =
    [
        "Utilities",
        "Traders",
        "Transport",
        "Producers",
        "Industry",
        "Intercompany",
    ];

    private static readonly string[] TradingAreas = ["GTF", "THE", "ETF", "JBZ"];

    public static DevelopmentSeedData Create()
    {
        var faker = DevelopmentDataSeeder.CreateFaker();
        var companies = CreateCompanies();
        var counterparties = CreateCounterparties();
        var contracts = CreateContracts(faker, companies, counterparties);
        return new(
            companies,
            counterparties,
            CreateTradingPoints(),
            contracts,
            CreateDeliveries(faker, contracts),
            CreateCapacityBookings(faker, contracts),
            CreateTransfers(faker, contracts),
            CreateBiotickets(faker, contracts),
            CreateTaxTariffs(faker, contracts),
            CreateHedges(faker, contracts),
            CreateMarketPrices(faker),
            CreateCertificateTransactions(faker, contracts, counterparties)
        );
    }

    private static List<SeedCompany> CreateCompanies() =>
        [
            new(
                DevelopmentDataSeeder.Id("company", 1),
                "BGDK",
                "BioGem Denmark A/S",
                "DK",
                45,
                0.25m,
                "DKK"
            ),
            new(
                DevelopmentDataSeeder.Id("company", 2),
                "BGSE",
                "BioGem Sweden AB",
                "SE",
                46,
                0.25m,
                "SEK"
            ),
            new(
                DevelopmentDataSeeder.Id("company", 3),
                "BGDE",
                "BioGem Germany GmbH",
                "DE",
                49,
                0.19m,
                "EUR"
            ),
        ];

    private static List<SeedCounterparty> CreateCounterparties()
    {
        (string Name, string Shorthand, string CountryCode, short DialCode)[] rows =
        [
            ("Vattenfall Energy Trading", "VATT", "SE", 46),
            ("Statkraft Markets", "STAT", "NO", 47),
            ("Gasunie Transport", "GASU", "NL", 31),
            ("BioGem Nordics AB", "BGNO", "SE", 46),
            ("Uniper Global Commodities", "UNIP", "DE", 49),
            ("Ørsted Salg & Service", "ORST", "DK", 45),
            ("Shell Energy Europe", "SHEL", "NL", 31),
            ("Equinor ASA", "EQUI", "NO", 47),
            ("RWE Supply & Trading", "RWES", "DE", 49),
            ("Engie Global Markets", "ENGI", "FR", 33),
            ("Fortum Market Oy", "FORT", "FI", 358),
            ("Axpo Solutions AG", "AXPO", "CH", 41),
            ("DCC Energi A/S", "DCCE", "DK", 45),
            ("Alpiq Nordic", "ALPI", "CH", 41),
            ("Neste Renewables", "NEST", "FI", 358),
            ("Gasum Oy", "GASM", "FI", 358),
        ];
        return rows.Select(
                (row, index) =>
                    new SeedCounterparty(
                        DevelopmentDataSeeder.Id("counterparty", index + 1),
                        row.Name,
                        row.Shorthand,
                        Segments[index % Segments.Length],
                        row.CountryCode,
                        row.DialCode,
                        true
                    )
            )
            .ToList();
    }

    private static List<SeedTradingPoint> CreateTradingPoints()
    {
        (string Code, string Type, string Country, string Start, string End)[] rows =
        [
            ("GTF", "VIRTUAL", "Denmark", "GTF", "GTF"),
            ("ETF", "VIRTUAL", "Denmark", "ETF", "ETF"),
            ("THE", "VIRTUAL", "Germany", "THE", "THE"),
            ("JBZ", "ENTRY", "Denmark", "JBZ", "GTF"),
            ("RES", "ENTRY", "Denmark", "RES", "GTF"),
            ("CSP", "EXIT", "Sweden", "GTF", "CSP"),
        ];
        return rows.Select(
                (row, index) =>
                    new SeedTradingPoint(
                        DevelopmentDataSeeder.Id("trading-point", index + 1),
                        row.Code,
                        row.Type,
                        string.Concat(row.Code, " gas network point"),
                        row.Country,
                        row.Type == "EXIT" ? "Exit" : "Entry",
                        row.Code,
                        row.Start,
                        row.End
                    )
            )
            .ToList();
    }

    private static List<SeedContract> CreateContracts(
        Faker faker,
        IReadOnlyList<SeedCompany> companies,
        IReadOnlyList<SeedCounterparty> counterparties
    ) =>
        Enumerable
            .Range(0, 24)
            .Select(index => CreateContract(faker, companies, counterparties, index))
            .ToList();

    private static SeedContract CreateContract(
        Faker faker,
        IReadOnlyList<SeedCompany> companies,
        IReadOnlyList<SeedCounterparty> counterparties,
        int index
    )
    {
        var isTicket = index >= 16;
        var counterparty = counterparties[index % counterparties.Count];
        var action =
            isTicket ? (index % 2 == 0 ? "Buy" : "Sell")
            : index % 5 == 4 ? "Intercompany"
            : index % 2 == 0 ? "Buy"
            : "Sell";
        var code = isTicket
            ? (action == "Buy" ? "BT" : "ST")
            : action switch
            {
                "Buy" => "BG",
                "Sell" => "SG",
                _ => "IM",
            };
        var suffix = isTicket ? "CO2E" : "NOQS";
        var company = companies[index % companies.Count];
        return new(
            DevelopmentDataSeeder.Id("contract", index + 1),
            string.Concat(
                counterparty.Shorthand,
                ".",
                counterparty.CountryDialCode,
                ".",
                code,
                ".26",
                (index + 1).ToString("D2", System.Globalization.CultureInfo.InvariantCulture),
                ".",
                suffix
            ),
            company.Shorthand,
            company.CountryCode,
            company.CountryDialCode,
            (short)(index + 1),
            (short)2026,
            counterparty.Id,
            action == "Sell" ? null : company.Id,
            action == "Buy" ? null : company.Id,
            string.Concat("BG-", (index % 3) + 1),
            TradingAreas[index % TradingAreas.Length],
            isTicket ? "Tickets" : "Gas",
            action,
            isTicket ? null : "NOQ",
            isTicket ? "None" : "SUB",
            isTicket ? null
                : index % 3 == 0 ? "TTF"
                : "FIXED",
            isTicket ? null : decimal.Round(faker.Random.Decimal(24m, 56m), 4),
            isTicket ? "FIXED" : null,
            isTicket ? decimal.Round(faker.Random.Decimal(65m, 130m), 4) : null,
            "Running month + X",
            "Calender day",
            (short)10,
            (short)30,
            "Fixed",
            false,
            !isTicket,
            isTicket,
            action == "Intercompany" ? "Intercompany" : "External",
            faker.Lorem.Sentence(6)
        );
    }

    private static List<SeedDelivery> CreateDeliveries(
        Faker faker,
        IReadOnlyList<SeedContract> contracts
    )
    {
        var rows = new List<SeedDelivery>();
        var gasContracts = contracts.Where(contract => contract.ProductType == "Gas").ToList();
        for (var contractIndex = 0; contractIndex < gasContracts.Count; contractIndex++)
        {
            for (var monthIndex = 0; monthIndex < 3; monthIndex++)
            {
                var contract = gasContracts[contractIndex];
                var nominated = decimal.Round(faker.Random.Decimal(3_600m, 16_000m), 3);
                var realised = decimal.Round(nominated * faker.Random.Decimal(0.93m, 1.02m), 3);
                var unitPrice = contract.FixedPriceGasEurMwh ?? 37m;
                var subtotal = decimal.Round(realised * unitPrice, 2);
                var vat = decimal.Round(subtotal * 0.25m, 2);
                var status = Statuses[(contractIndex + monthIndex) % Statuses.Length];
                var supplyMonth = new DateOnly(2026, 1 + monthIndex, 1);
                rows.Add(
                    new(
                        DevelopmentDataSeeder.Id("delivery", rows.Count + 1),
                        contract.Id,
                        contract.Action == "Buy" ? "Sourcing"
                            : contract.Action == "Sell" ? "Sales"
                            : "Intercompany",
                        supplyMonth,
                        status,
                        contract.BalancingGroup,
                        contract.NetworkPoint,
                        decimal.Round(faker.Random.Decimal(8m, 28m), 3),
                        nominated,
                        realised,
                        realised,
                        contract.PriceMechanismGas ?? "FIXED",
                        "Fixed",
                        contract.ProductType,
                        unitPrice,
                        contract.Action == "Buy" ? 0m : subtotal,
                        subtotal,
                        0.25m,
                        vat,
                        subtotal + vat,
                        status == "Pending - No Invoice"
                            ? null
                            : supplyMonth.AddMonths(1).AddDays(7),
                        supplyMonth.AddMonths(2),
                        faker.Lorem.Sentence(5)
                    )
                );
            }
        }
        return rows;
    }

    private static List<SeedCapacityBooking> CreateCapacityBookings(
        Faker faker,
        IReadOnlyList<SeedContract> contracts
    ) =>
        contracts
            .Where(contract => contract.ProductType == "Gas")
            .Take(10)
            .Select(
                (contract, index) =>
                {
                    var capacity = decimal.Round(faker.Random.Decimal(6m, 25m), 3);
                    var price = decimal.Round(faker.Random.Decimal(0.8m, 4.5m), 4);
                    return new SeedCapacityBooking(
                        DevelopmentDataSeeder.Id("capacity-booking", index + 1),
                        contract.Id,
                        new DateOnly(2026, 4, 1),
                        contract.BalancingGroup,
                        contract.CounterpartyId,
                        index % 2 == 0 ? "GTF/THE - Monthly" : "THE/GTF - Monthly",
                        index % 2 == 0 ? "GTF" : "THE",
                        index % 2 == 0 ? "THE" : "GTF",
                        capacity,
                        price,
                        decimal.Round(capacity * price * 720m, 2),
                        faker.Lorem.Sentence(5)
                    );
                }
            )
            .ToList();

    private static List<SeedTransfer> CreateTransfers(
        Faker faker,
        IReadOnlyList<SeedContract> contracts
    ) =>
        contracts
            .Where(contract => contract.ProductType == "Gas")
            .Skip(4)
            .Take(10)
            .Select(
                (contract, index) =>
                {
                    var capacity = decimal.Round(faker.Random.Decimal(5m, 20m), 3);
                    var volume = decimal.Round(faker.Random.Decimal(2_000m, 12_000m), 3);
                    return new SeedTransfer(
                        DevelopmentDataSeeder.Id("transfer", index + 1),
                        contract.Id,
                        new DateOnly(2026, 5, 1),
                        contract.BalancingGroup,
                        contract.CounterpartyId,
                        TradingAreas[index % TradingAreas.Length],
                        capacity,
                        decimal.Round(capacity * 0.95m, 3),
                        volume,
                        decimal.Round(volume * faker.Random.Decimal(-0.04m, 0.04m), 3),
                        index % 2 == 0 ? "TTF" : "FIXED",
                        decimal.Round(faker.Random.Decimal(0.3m, 1.4m), 4),
                        decimal.Round(faker.Random.Decimal(0.5m, 2.2m), 4),
                        Statuses[index % Statuses.Length],
                        faker.Lorem.Sentence(5)
                    );
                }
            )
            .ToList();

    private static List<SeedBioticket> CreateBiotickets(
        Faker faker,
        IReadOnlyList<SeedContract> contracts
    )
    {
        var rows = new List<SeedBioticket>();
        foreach (var contract in contracts.Where(contract => contract.ProductType == "Tickets"))
        {
            for (var monthIndex = 0; monthIndex < 2; monthIndex++)
            {
                var volume = decimal.Round(faker.Random.Decimal(120m, 850m), 3);
                var price = contract.FixedPriceTicketEurTon ?? 85m;
                var subtotal = decimal.Round(volume * price, 2);
                var vat = decimal.Round(subtotal * 0.25m, 2);
                var month = new DateOnly(2026, monthIndex + 1, 1);
                rows.Add(
                    new(
                        DevelopmentDataSeeder.Id("bioticket", rows.Count + 1),
                        contract.Id,
                        contract.Action == "Buy" ? "Sourcing" : "Sales",
                        month,
                        decimal.Round(volume * 1.02m, 3),
                        volume,
                        volume,
                        price,
                        contract.Action == "Sell" ? subtotal : 0m,
                        subtotal,
                        0.25m,
                        vat,
                        subtotal + vat,
                        "Traders",
                        (short)2026,
                        month,
                        Statuses[rows.Count % Statuses.Length],
                        faker.Lorem.Sentence(5)
                    )
                );
            }
        }
        return rows;
    }

    private static List<SeedTaxTariff> CreateTaxTariffs(
        Faker faker,
        IReadOnlyList<SeedContract> contracts
    ) =>
        contracts
            .Where(contract => contract.ProductType == "Gas")
            .Take(10)
            .Select(
                (contract, index) =>
                    new SeedTaxTariff(
                        DevelopmentDataSeeder.Id("tax-tariff", index + 1),
                        contract.Id,
                        contract.CounterpartyId,
                        new DateOnly(2026, 1, 1),
                        new DateOnly(2026, 12, 31),
                        decimal.Round(faker.Random.Decimal(0.1m, 0.9m), 4),
                        decimal.Round(faker.Random.Decimal(0.2m, 1.2m), 4),
                        decimal.Round(faker.Random.Decimal(0.1m, 0.8m), 4),
                        decimal.Round(faker.Random.Decimal(0.05m, 0.4m), 4),
                        decimal.Round(faker.Random.Decimal(0.05m, 0.3m), 4),
                        "EUR"
                    )
            )
            .ToList();

    private static List<SeedHedge> CreateHedges(
        Faker faker,
        IReadOnlyList<SeedContract> contracts
    ) =>
        contracts
            .Where(contract => contract.ProductType == "Gas")
            .Take(12)
            .Select(
                (contract, index) =>
                    new SeedHedge(
                        DevelopmentDataSeeder.Id("hedge", index + 1),
                        contract.Id,
                        new DateOnly(2026, (index % 6) + 1, 1),
                        decimal.Round(faker.Random.Decimal(1_000m, 8_000m), 3),
                        decimal.Round(faker.Random.Decimal(28m, 52m), 4)
                    )
            )
            .ToList();

    private static List<SeedMarketPrice> CreateMarketPrices(Faker faker) =>
        Enumerable
            .Range(0, 180)
            .Select(index =>
            {
                var ttf = decimal.Round(faker.Random.Decimal(26m, 49m), 4);
                return new SeedMarketPrice(
                    new DateOnly(2026, 1, 1).AddDays(index),
                    ttf,
                    decimal.Round(ttf * faker.Random.Decimal(0.94m, 1.04m), 4),
                    decimal.Round(ttf * faker.Random.Decimal(0.96m, 1.06m), 4),
                    decimal.Round(ttf * faker.Random.Decimal(0.91m, 1.08m), 4),
                    decimal.Round(ttf * faker.Random.Decimal(0.9m, 1.07m), 4),
                    decimal.Round(faker.Random.Decimal(58m, 86m), 4),
                    decimal.Round(ttf * faker.Random.Decimal(0.98m, 1.08m), 4),
                    decimal.Round(faker.Random.Decimal(10.8m, 11.8m), 4),
                    decimal.Round(faker.Random.Decimal(0.91m, 0.98m), 4),
                    decimal.Round(faker.Random.Decimal(0.82m, 0.88m), 4),
                    decimal.Round(faker.Random.Decimal(1.04m, 1.17m), 4),
                    decimal.Round(faker.Random.Decimal(7.44m, 7.47m), 4)
                );
            })
            .ToList();

    private static List<SeedCertificateTransaction> CreateCertificateTransactions(
        Faker faker,
        IReadOnlyList<SeedContract> contracts,
        IReadOnlyList<SeedCounterparty> counterparties
    ) =>
        Enumerable
            .Range(0, 12)
            .Select(index =>
            {
                var producer = contracts[(index * 2) % 16];
                var customer = contracts[((index * 2) + 1) % 16];
                var producerCounterparty = counterparties.First(row =>
                    row.Id == producer.CounterpartyId
                );
                var customerCounterparty = counterparties.First(row =>
                    row.Id == customer.CounterpartyId
                );
                var volume = decimal.Round(faker.Random.Decimal(800m, 9_000m), 3);
                var start = new DateOnly(2026, (index % 6) + 1, 1);
                return new SeedCertificateTransaction(
                    DevelopmentDataSeeder.Id("certificate-transaction", index + 1),
                    string.Concat(
                        "SF-DEV-",
                        (index + 1).ToString(
                            "D4",
                            System.Globalization.CultureInfo.InvariantCulture
                        )
                    ),
                    string.Concat("Guarantee of Origin #", index + 1),
                    string.Concat(
                        "CERT-2026-",
                        (index + 1).ToString(
                            "D4",
                            System.Globalization.CultureInfo.InvariantCulture
                        )
                    ),
                    producerCounterparty.CountryCode,
                    producer.Id,
                    producerCounterparty.Name,
                    customer.Id,
                    customerCounterparty.Name,
                    start,
                    start.AddMonths(1).AddDays(6),
                    index % 4 == 0 ? "Processing" : "Completed",
                    start.AddMonths(1),
                    volume,
                    volume,
                    customerCounterparty.Name,
                    start,
                    start.AddMonths(1).AddDays(-1),
                    string.Concat("Biogas plant ", (index % 5) + 1),
                    "Biogas",
                    faker.Lorem.Sentence(6)
                );
            })
            .ToList();
}

internal sealed record SeedCompany(
    Guid Id,
    string Shorthand,
    string Name,
    string CountryCode,
    short CountryDialCode,
    decimal VatRate,
    string DefaultCurrency
);

internal sealed record SeedCounterparty(
    Guid Id,
    string Name,
    string Shorthand,
    string Segment,
    string CountryCode,
    short CountryDialCode,
    bool VatApplicable
);

internal sealed record SeedTradingPoint(
    Guid Id,
    string Code,
    string Type,
    string Description,
    string Country,
    string Action,
    string Name,
    string StartArea,
    string EndArea
);

internal sealed record SeedContract(
    Guid Id,
    string ContractName,
    string CompanyShorthand,
    string CountryCode,
    short CountryDialCode,
    short ContractNumber,
    short YearOfContract,
    Guid CounterpartyId,
    Guid? SourcingCenter,
    Guid? SalesCenter,
    string BalancingGroup,
    string NetworkPoint,
    string ProductType,
    string Action,
    string? GooQuality,
    string SubsidyStatus,
    string? PriceMechanismGas,
    decimal? FixedPriceGasEurMwh,
    string? PriceMechanismTicket,
    decimal? FixedPriceTicketEurTon,
    string InvoicingMechanism,
    string PaymentMechanism,
    short DaysToInvoiceAfterDelivery,
    short DaysToPaymentAfterInvoice,
    string DeliveryType,
    bool IncludesGoo,
    bool IncludesGas,
    bool IncludesTicket,
    string ContractType,
    string Comment
);

internal sealed record SeedDelivery(
    Guid Id,
    Guid ContractId,
    string BookType,
    DateOnly SupplyMonth,
    string Status,
    string BalancingGroup,
    string TradingArea,
    decimal CapacityMw,
    decimal VolumeNominatedMwh,
    decimal VolumeRealisedMwh,
    decimal VolumeMwh,
    string PriceMechanism,
    string DeliveryType,
    string Product,
    decimal CostEurMwh,
    decimal RevenueEur,
    decimal SubtotalEur,
    decimal VatPct,
    decimal VatEur,
    decimal InvoiceAmountEur,
    DateOnly? InvoiceDate,
    DateOnly PaymentDateForecast,
    string TraderComment
);

internal sealed record SeedCapacityBooking(
    Guid Id,
    Guid ContractId,
    DateOnly SupplyMonth,
    string BalancingGroup,
    Guid CounterpartyId,
    string PriceMechanism,
    string StartArea,
    string EndArea,
    decimal CapacityMw,
    decimal CapacityPriceEurMwh,
    decimal CapacityCostEur,
    string Comments
);

internal sealed record SeedTransfer(
    Guid Id,
    Guid ContractId,
    DateOnly SupplyMonth,
    string BalancingGroup,
    Guid CounterpartyId,
    string TradingArea,
    decimal CapacityMw,
    decimal BookedCapacityMw,
    decimal VolumeMwh,
    decimal BalancingEffectMwh,
    string PriceMechanism,
    decimal TransportCostEurMwh,
    decimal CapacityCostEurMwh,
    string Status,
    string Comments
);

internal sealed record SeedBioticket(
    Guid Id,
    Guid ContractId,
    string BookType,
    DateOnly ContractMonth,
    decimal VolumeNominatedTon,
    decimal VolumeRealisedTon,
    decimal VolumeTon,
    decimal CostEurTon,
    decimal RevenueEur,
    decimal SubtotalEur,
    decimal VatPct,
    decimal VatEur,
    decimal InvoiceAmountEur,
    string CounterpartySegment,
    short Year,
    DateOnly DeliveryMonth,
    string Status,
    string TraderComment
);

internal sealed record SeedTaxTariff(
    Guid Id,
    Guid ContractId,
    Guid CounterpartyId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal TaxLocalCurMwh,
    decimal TsoLocalCurMwh,
    decimal DsoLocalCurMwh,
    decimal AdmFeeLocalCurMwh,
    decimal BalFeeLocalCurMwh,
    string Currency
);

internal sealed record SeedHedge(
    Guid Id,
    Guid ContractId,
    DateOnly Month,
    decimal HedgeAmountMwh,
    decimal HedgePriceEurMwh
);

internal sealed record SeedMarketPrice(
    DateOnly PriceDate,
    decimal TtfEurMwh,
    decimal EgsiEtfEurMwh,
    decimal TheEurMwh,
    decimal BgoEurMwh,
    decimal PgoEurMwh,
    decimal EuaEurMwh,
    decimal WithinDayMktEurMwh,
    decimal EurSek,
    decimal EurChf,
    decimal EurGbp,
    decimal EurUsd,
    decimal EurDkk
);

internal sealed record SeedCertificateTransaction(
    Guid Id,
    string SalesforceTransactionId,
    string TransactionName,
    string CertificateTransactionId,
    string CountryOfProduction,
    Guid ProducerContractId,
    string ProducerCompany,
    Guid CustomerContractId,
    string CustomerCompany,
    DateOnly ProductionDate,
    DateOnly IssueDate,
    string Status,
    DateOnly TransactionStartDate,
    decimal TransactionVolumeMwh,
    decimal VolumeMwh,
    string BeneficiaryName,
    DateOnly ConsumptionPeriodStart,
    DateOnly ConsumptionPeriodEnd,
    string ProductionDeviceName,
    string EnergySource,
    string Text
);
