using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Bogus;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Tradebook.Infrastructure.Data;

namespace Tradebook.Infrastructure.Development;

#pragma warning disable MA0051 // Seed orchestration stays grouped by transaction and table family.
#pragma warning disable MA0048 // Logger-message helper intentionally stays beside its sole caller.

/// <summary>Creates a representative, deterministic dataset for an empty development database.</summary>
public sealed class DevelopmentDataSeeder(
    INpgsqlConnectionFactory connections,
    ILogger<DevelopmentDataSeeder> logger
)
{
    private const int Seed = 20_260_810;
    private const string SeedNamespace = "tradebook-development-seed-v1";

    public async Task<bool> SeedIfEmptyAsync(CancellationToken cancellationToken)
    {
        var connection = await connections
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                .ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                await connection
                    .ExecuteAsync(
                        new CommandDefinition(
                            "SELECT pg_advisory_xact_lock(hashtext('tradebook-development-seed-v1'))",
                            transaction: transaction,
                            cancellationToken: cancellationToken
                        )
                    )
                    .ConfigureAwait(false);

                var hasContracts = await connection
                    .ExecuteScalarAsync<bool>(
                        new CommandDefinition(
                            "SELECT EXISTS(SELECT 1 FROM contracts)",
                            transaction: transaction,
                            cancellationToken: cancellationToken
                        )
                    )
                    .ConfigureAwait(false);
                if (hasContracts)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    DevelopmentSeedLog.Skipped(logger);
                    return false;
                }

                await connection
                    .ExecuteAsync(
                        new CommandDefinition(
                            "SELECT set_config('app.actor_id', @ActorId, true)",
                            new { ActorId = Guid.Empty.ToString() },
                            transaction,
                            cancellationToken: cancellationToken
                        )
                    )
                    .ConfigureAwait(false);

                var data = DevelopmentSeedData.Create();
                await InsertMasterDataAsync(connection, transaction, data, cancellationToken)
                    .ConfigureAwait(false);
                await InsertTradingDataAsync(connection, transaction, data, cancellationToken)
                    .ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                DevelopmentSeedLog.Completed(
                    logger,
                    data.Contracts.Count,
                    data.Deliveries.Count,
                    data.MarketPrices.Count
                );
                return true;
            }
        }
    }

    private static async Task InsertMasterDataAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DevelopmentSeedData data,
        CancellationToken cancellationToken
    )
    {
        await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO companies (id, shorthand, name, country_code, country_dial_code, vat_rate, default_currency)
                VALUES (@Id, @Shorthand, @Name, @CountryCode, @CountryDialCode, @VatRate, @DefaultCurrency)
                """,
                data.Companies,
                cancellationToken
            )
            .ConfigureAwait(false);
        await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO counterparties (id, name, shorthand, segment, country_code, country_dial_code, vat_applicable)
                VALUES (@Id, @Name, @Shorthand, CAST(@Segment AS segment_enum), @CountryCode, @CountryDialCode, @VatApplicable)
                """,
                data.Counterparties,
                cancellationToken
            )
            .ConfigureAwait(false);
        await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO trading_points (id, code, type, description, country, action, name, start_area, end_area)
                VALUES (@Id, @Code, CAST(@Type AS point_type_enum), @Description, @Country, @Action, @Name, @StartArea, @EndArea)
                """,
                data.TradingPoints,
                cancellationToken
            )
            .ConfigureAwait(false);
        await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO contracts (
                    id, contract_name, company_shorthand, country_code, country_dial_code,
                    contract_number, year_of_contract, counterparty_id, sourcing_center, sales_center,
                    balancing_group, network_point, product_type, action, goo_quality, subsidy_status,
                    price_mechanism_gas, fixed_price_gas_eur_mwh, price_mechanism_ticket,
                    fixed_price_ticket_eur_ton, invoicing_mechanism, payment_mechanism,
                    days_to_invoice_after_delivery, days_to_payment_after_invoice, delivery_type,
                    includes_goo, includes_gas, includes_ticket, contract_type, comment)
                VALUES (
                    @Id, @ContractName, @CompanyShorthand, @CountryCode, @CountryDialCode,
                    @ContractNumber, @YearOfContract, @CounterpartyId, @SourcingCenter, @SalesCenter,
                    @BalancingGroup, @NetworkPoint, CAST(@ProductType AS product_type_enum),
                    CAST(@Action AS action_enum), CAST(@GooQuality AS goo_quality_enum),
                    CAST(@SubsidyStatus AS subsidy_status_enum), CAST(@PriceMechanismGas AS gas_price_mech_enum),
                    @FixedPriceGasEurMwh, CAST(@PriceMechanismTicket AS price_mech_enum),
                    @FixedPriceTicketEurTon, CAST(@InvoicingMechanism AS invoicing_mech_enum),
                    CAST(@PaymentMechanism AS payment_mech_enum), @DaysToInvoiceAfterDelivery,
                    @DaysToPaymentAfterInvoice, CAST(@DeliveryType AS delivery_type_enum),
                    @IncludesGoo, @IncludesGas, @IncludesTicket,
                    CAST(@ContractType AS contract_type_enum), @Comment)
                """,
                data.Contracts,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task InsertTradingDataAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DevelopmentSeedData data,
        CancellationToken cancellationToken
    )
    {
        await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO physical_deliveries (
                    id, contract_id, contract_instance_id, book_type, supply_month, status,
                    balancing_group, trading_area, capacity_mw, volume_nominated_mwh,
                    volume_realised_mwh, volume_mwh, price_mechanism, delivery_type, product,
                    cost_eur_mwh, revenue_eur, subtotal_eur, vat_pct, vat_eur,
                    invoice_amount_eur, invoice_date, payment_date_forecast, trader_comment)
                VALUES (
                    @Id, @ContractId, '', CAST(@BookType AS book_type_enum), @SupplyMonth,
                    CAST(@Status AS report_status_enum), @BalancingGroup, @TradingArea, @CapacityMw,
                    @VolumeNominatedMwh, @VolumeRealisedMwh, @VolumeMwh,
                    CAST(@PriceMechanism AS gas_price_mech_enum), CAST(@DeliveryType AS delivery_type_enum),
                    CAST(@Product AS product_type_enum), @CostEurMwh, @RevenueEur, @SubtotalEur,
                    @VatPct, @VatEur, @InvoiceAmountEur, @InvoiceDate, @PaymentDateForecast, @TraderComment)
                """,
                data.Deliveries,
                cancellationToken
            )
            .ConfigureAwait(false);
        await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO capacity_bookings (
                    id, contract_id, contract_instance_id, supply_month, balancing_group,
                    counterparty_id, price_mechanism, start_area, end_area, capacity_mw,
                    capacity_price_eur_mwh, capacity_cost_eur, comments)
                VALUES (
                    @Id, @ContractId, '', @SupplyMonth, @BalancingGroup, @CounterpartyId,
                    CAST(@PriceMechanism AS capacity_price_mech_enum), @StartArea, @EndArea,
                    @CapacityMw, @CapacityPriceEurMwh, @CapacityCostEur, @Comments)
                """,
                data.CapacityBookings,
                cancellationToken
            )
            .ConfigureAwait(false);
        await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO transfers (
                    id, contract_id, contract_instance_id, supply_month, balancing_group,
                    counterparty_id, trading_area, capacity_mw, booked_capacity_mw, volume_mwh,
                    balancing_effect_mwh, price_mechanism, transport_cost_eur_mwh,
                    capacity_cost_eur_mwh, status, comments)
                VALUES (
                    @Id, @ContractId, '', @SupplyMonth, @BalancingGroup, @CounterpartyId,
                    @TradingArea, @CapacityMw, @BookedCapacityMw, @VolumeMwh, @BalancingEffectMwh,
                    CAST(@PriceMechanism AS gas_price_mech_enum), @TransportCostEurMwh,
                    @CapacityCostEurMwh, CAST(@Status AS report_status_enum), @Comments)
                """,
                data.Transfers,
                cancellationToken
            )
            .ConfigureAwait(false);
        await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO bioticket_deliveries (
                    id, contract_id, contract_instance_id, book_type, contract_month,
                    volume_nominated_ton, volume_realised_ton, volume_ton, cost_eur_ton,
                    revenue_eur, subtotal_eur, vat_pct, vat_eur, invoice_amount_eur,
                    counterparty_segment, year, delivery_month, status, trader_comment)
                VALUES (
                    @Id, @ContractId, '', CAST(@BookType AS book_type_enum), @ContractMonth,
                    @VolumeNominatedTon, @VolumeRealisedTon, @VolumeTon, @CostEurTon,
                    @RevenueEur, @SubtotalEur, @VatPct, @VatEur, @InvoiceAmountEur,
                    CAST(@CounterpartySegment AS segment_enum), @Year, @DeliveryMonth,
                    CAST(@Status AS report_status_enum), @TraderComment)
                """,
                data.Biotickets,
                cancellationToken
            )
            .ConfigureAwait(false);
        await InsertReferenceAndReportingDataAsync(connection, transaction, data, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task InsertReferenceAndReportingDataAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DevelopmentSeedData data,
        CancellationToken cancellationToken
    )
    {
        await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO tax_tariffs (
                    id, contract_id, counterparty_id, period_start, period_end,
                    tax_local_cur_mwh, tso_local_cur_mwh, dso_local_cur_mwh,
                    adm_fee_local_cur_mwh, bal_fee_local_cur_mwh, currency)
                VALUES (
                    @Id, @ContractId, @CounterpartyId, @PeriodStart, @PeriodEnd,
                    @TaxLocalCurMwh, @TsoLocalCurMwh, @DsoLocalCurMwh,
                    @AdmFeeLocalCurMwh, @BalFeeLocalCurMwh, @Currency)
                """,
                data.TaxTariffs,
                cancellationToken
            )
            .ConfigureAwait(false);
        await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO hedges (id, contract_id, month, hedge_amount_mwh, hedge_price_eur_mwh)
                VALUES (@Id, @ContractId, @Month, @HedgeAmountMwh, @HedgePriceEurMwh)
                """,
                data.Hedges,
                cancellationToken
            )
            .ConfigureAwait(false);
        await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO market_prices (
                    price_date, ttf_eur_mwh, egsi_etf_eur_mwh, the_eur_mwh,
                    bgo_eur_mwh, pgo_eur_mwh, eua_eur_mwh, within_day_mkt_eur_mwh,
                    eur_sek, eur_chf, eur_gbp, eur_usd, eur_dkk)
                VALUES (
                    @PriceDate, @TtfEurMwh, @EgsiEtfEurMwh, @TheEurMwh,
                    @BgoEurMwh, @PgoEurMwh, @EuaEurMwh, @WithinDayMktEurMwh,
                    @EurSek, @EurChf, @EurGbp, @EurUsd, @EurDkk)
                """,
                data.MarketPrices,
                cancellationToken
            )
            .ConfigureAwait(false);
        await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO goo_certificate_transactions (
                    id, sf_transaction_id, transaction_name, certificate_transaction_id,
                    country_of_production, producer_contract_id, producer_company,
                    customer_contract_id, customer_company, production_date, issue_date,
                    status, transaction_start_date, transaction_volume_mwh, volume_mwh,
                    beneficiary_name, consumption_period_start, consumption_period_end,
                    production_device_name, energy_source, text)
                VALUES (
                    @Id, @SalesforceTransactionId, @TransactionName, @CertificateTransactionId,
                    @CountryOfProduction, @ProducerContractId, @ProducerCompany,
                    @CustomerContractId, @CustomerCompany, @ProductionDate, @IssueDate,
                    CAST(@Status AS transaction_status_enum), @TransactionStartDate,
                    @TransactionVolumeMwh, @VolumeMwh, @BeneficiaryName,
                    @ConsumptionPeriodStart, @ConsumptionPeriodEnd,
                    @ProductionDeviceName, @EnergySource, @Text)
                """,
                data.CertificateTransactions,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static Task<int> ExecuteAsync<T>(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        IReadOnlyCollection<T> rows,
        CancellationToken cancellationToken
    ) =>
        connection.ExecuteAsync(
            new CommandDefinition(sql, rows, transaction, cancellationToken: cancellationToken)
        );

    internal static Guid Id(string entity, int index)
    {
        var name = string.Concat(
            SeedNamespace,
            ":",
            entity,
            ":",
            index.ToString(CultureInfo.InvariantCulture)
        );
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(name)).AsSpan(0, 16));
    }

    internal static Faker CreateFaker() =>
        new("en")
        {
            Random = new Randomizer(Seed),
            DateTimeReference = new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc),
        };
}

internal static partial class DevelopmentSeedLog
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Development seed skipped; contracts already exist."
    )]
    public static partial void Skipped(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Development seed created {ContractCount} contracts, {DeliveryCount} deliveries, and {MarketPriceCount} market prices."
    )]
    public static partial void Completed(
        ILogger logger,
        int contractCount,
        int deliveryCount,
        int marketPriceCount
    );
}
