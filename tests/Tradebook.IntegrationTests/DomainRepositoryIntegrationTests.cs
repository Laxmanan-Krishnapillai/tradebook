using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using Tradebook.Core.Domain;
using Tradebook.Core.DTOs;
using Tradebook.Infrastructure.Data;
using Tradebook.Infrastructure.Options;

namespace Tradebook.IntegrationTests;

public sealed class DomainRepositoryIntegrationTests(PostgresTestFixture postgres)
    : PostgresDatabaseTestBase(postgres)
{
    private static readonly string[] Task02AggregateTypes =
    [
        OutboxAggregateTypes.Contract,
        OutboxAggregateTypes.CapacityBooking,
        OutboxAggregateTypes.Transfer,
        OutboxAggregateTypes.BioticketDelivery,
        OutboxAggregateTypes.GooCertificateTransaction,
        OutboxAggregateTypes.MarketPrice,
        OutboxAggregateTypes.TaxTariff,
        OutboxAggregateTypes.Hedge,
    ];

    [Fact]
    public async Task EveryTask02RepositoryWritesARealTransactionalOutboxEvent()
    {
        var actorId = Guid.NewGuid();
        var counterpartyId = await CreateCounterpartyAsync();
        await using var connections = new NpgsqlConnectionFactory(
            Options.Create(new DatabaseOptions { ConnectionString = Postgres.ConnectionString })
        );

        var primary = await CreatePrimaryTask02RecordsAsync(connections, actorId, counterpartyId);
        var remaining = await CreateRemainingTask02RecordsAsync(
            connections,
            actorId,
            counterpartyId,
            primary.ContractId
        );

        Assert.Equal($"{primary.ContractName}-1-2026", primary.CapacityContractInstanceId);
        Assert.Equal($"{primary.ContractName}-2-2026", primary.TransferContractInstanceId);
        Assert.Equal($"{primary.ContractName}-3-2026", primary.BioticketContractInstanceId);

        await AssertHedgeVersioningAsync(
            connections,
            actorId,
            remaining.HedgeId,
            remaining.HedgeVersion
        );
        await AssertTask02OutboxAsync(actorId);

        Assert.NotEqual(Guid.Empty, remaining.GooId);
        Assert.NotEqual(Guid.Empty, remaining.TaxId);
    }

    private static async Task<PrimaryTask02Records> CreatePrimaryTask02RecordsAsync(
        NpgsqlConnectionFactory connections,
        Guid actorId,
        Guid counterpartyId
    )
    {
        var contract = await new ContractRepository(connections)
            .CreateAtomicAsync(BuildTask02ContractRequest(counterpartyId), actorId, default)
            .ConfigureAwait(false);
        var capacity = await new CapacityBookingRepository(connections)
            .CreateAtomicAsync(
                BuildTask02CapacityRequest(contract.ContractId, counterpartyId),
                actorId,
                default
            )
            .ConfigureAwait(false);
        var transfer = await new TransferRepository(connections)
            .CreateAtomicAsync(
                BuildTask02TransferRequest(contract.ContractId, counterpartyId),
                actorId,
                default
            )
            .ConfigureAwait(false);
        var bioticket = await new BioticketRepository(connections)
            .CreateAtomicAsync(BuildTask02BioticketRequest(contract.ContractId), actorId, default)
            .ConfigureAwait(false);

        return new(
            contract.ContractId,
            contract.ContractName,
            capacity.ContractInstanceId,
            transfer.ContractInstanceId,
            bioticket.ContractInstanceId
        );
    }

    private static CreateContractRequest BuildTask02ContractRequest(Guid counterpartyId) =>
        new(
            $"REPO45.SG.{Guid.NewGuid():N}.NOQS",
            counterpartyId,
            "Gas",
            "Sell",
            "REPO",
            "DK",
            45,
            null,
            2026,
            null,
            null,
            "BGEM",
            null,
            "SUB",
            "TTF",
            null,
            "External",
            "repository integration"
        );

    private static CreateCapacityBookingRequest BuildTask02CapacityRequest(
        Guid contractId,
        Guid counterpartyId
    ) =>
        new(
            contractId,
            new DateOnly(2026, 1, 1),
            null,
            counterpartyId,
            "BGEM",
            null,
            "GTF",
            "THE",
            null,
            null,
            null,
            null,
            10m,
            2m,
            20m,
            null
        );

    private static CreateTransferRequest BuildTask02TransferRequest(
        Guid contractId,
        Guid counterpartyId
    ) =>
        new(
            contractId,
            new DateOnly(2026, 2, 1),
            null,
            counterpartyId,
            "BGEM",
            "GTF",
            10m,
            9m,
            100m,
            1m,
            null,
            null,
            "TTF",
            2m,
            1m,
            "Awaiting",
            null
        );

    private static CreateBioticketRequest BuildTask02BioticketRequest(Guid contractId) =>
        new(
            contractId,
            "Sales",
            new DateOnly(2026, 3, 1),
            null,
            null,
            null,
            10m,
            9m,
            9m,
            15m,
            135m,
            .25m,
            33.75m,
            168.75m,
            "Awaiting",
            null
        );

    private static async Task<RemainingTask02Records> CreateRemainingTask02RecordsAsync(
        NpgsqlConnectionFactory connections,
        Guid actorId,
        Guid counterpartyId,
        Guid contractId
    )
    {
        var goo = await new GooCertificateRepository(connections)
            .CreateAtomicAsync(BuildTask02GooRequest(contractId), actorId, default)
            .ConfigureAwait(false);
        var market = await new MarketPriceRepository(connections)
            .UpsertAtomicAsync(
                BuildMarketPriceRequest(
                    new DateOnly(2026, 4, 1),
                    ttfEurMwh: 30m,
                    version: 0,
                    eurSek: 7.5m,
                    eurDkk: 7.46m
                ),
                actorId,
                default
            )
            .ConfigureAwait(false);
        var tax = await new TaxTariffRepository(connections)
            .CreateAtomicAsync(BuildTask02TaxRequest(contractId, counterpartyId), actorId, default)
            .ConfigureAwait(false);
        var hedge = await new HedgeRepository(connections)
            .CreateAtomicAsync(
                new CreateHedgeRequest(contractId, new DateOnly(2026, 6, 1), 100m, 29m),
                actorId,
                default
            )
            .ConfigureAwait(false);

        Assert.NotNull(market);
        return new(goo.GooCertificateTransactionId, tax.TaxTariffId, hedge.HedgeId, hedge.Version);
    }

    private static CreateGooCertificateTransactionRequest BuildTask02GooRequest(Guid contractId) =>
        new(
            $"SF-{Guid.NewGuid():N}",
            "Repository event",
            null,
            null,
            "DK",
            contractId,
            "Producer",
            1m,
            new DateOnly(2026, 3, 1),
            null,
            null,
            "DENA",
            "Latest transaction",
            new DateOnly(2026, 3, 1),
            9m,
            9m,
            "Biogas",
            null
        );

    private static CreateTaxTariffRequest BuildTask02TaxRequest(
        Guid contractId,
        Guid counterpartyId
    ) =>
        new(
            contractId,
            counterpartyId,
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 5, 31),
            1m,
            2m,
            3m,
            4m,
            5m,
            6m,
            "SEK"
        );

    private static async Task AssertHedgeVersioningAsync(
        NpgsqlConnectionFactory connections,
        Guid actorId,
        Guid hedgeId,
        long hedgeVersion
    )
    {
        var repository = new HedgeRepository(connections);
        var updated = await repository
            .UpdateAtomicAsync(
                new UpdateHedgeRequest(hedgeId, 110m, 30m, hedgeVersion),
                actorId,
                default
            )
            .ConfigureAwait(false);
        Assert.NotNull(updated);
        Assert.Null(
            await repository
                .UpdateAtomicAsync(
                    new UpdateHedgeRequest(hedgeId, 120m, 31m, hedgeVersion),
                    actorId,
                    default
                )
                .ConfigureAwait(false)
        );
    }

    private async Task AssertTask02OutboxAsync(Guid actorId)
    {
        using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        var aggregateTypes = (
            await connection
                .QueryAsync<string>(
                    """
                    SELECT DISTINCT aggregate_type
                    FROM outbox_events
                    WHERE aggregate_type = ANY(@Expected)
                    """,
                    new { Expected = Task02AggregateTypes }
                )
                .ConfigureAwait(false)
        ).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(Task02AggregateTypes.Length, aggregateTypes.Count);
        foreach (var aggregateType in Task02AggregateTypes)
        {
            Assert.Contains(aggregateType, aggregateTypes);
        }

        var auditedActors = await connection
            .ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM audit_log WHERE actor_id = @ActorId",
                new { ActorId = actorId }
            )
            .ConfigureAwait(false);
        Assert.True(auditedActors >= 9);
    }

    private sealed record PrimaryTask02Records(
        Guid ContractId,
        string ContractName,
        string CapacityContractInstanceId,
        string TransferContractInstanceId,
        string BioticketContractInstanceId
    );

    private sealed record RemainingTask02Records(
        Guid GooId,
        Guid TaxId,
        Guid HedgeId,
        long HedgeVersion
    );

    [Fact]
    public async Task MarketPriceUpdatesArePartialVersionedAndCannotResurrectAConcurrentDelete()
    {
        var actorId = Guid.NewGuid();
        var priceDate = new DateOnly(2041, 7, 19);
        await using var connections = new NpgsqlConnectionFactory(
            Options.Create(new DatabaseOptions { ConnectionString = Postgres.ConnectionString })
        );
        var repository = new MarketPriceRepository(connections);

        var createdVersion = await CreateInitialMarketPriceAsync(repository, priceDate, actorId);
        var updatedVersion = await UpdateAndAssertPartialMarketPriceAsync(
            repository,
            priceDate,
            actorId,
            createdVersion
        );
        await AssertRejectedMarketPriceUpsertsAsync(repository, priceDate, actorId, createdVersion);

        await AssertConcurrentDeleteCannotBeResurrectedAsync(
            repository,
            priceDate,
            actorId,
            updatedVersion
        );
    }

    private static async Task<long> CreateInitialMarketPriceAsync(
        MarketPriceRepository repository,
        DateOnly priceDate,
        Guid actorId
    )
    {
        var created = await repository
            .UpsertAtomicAsync(
                BuildMarketPriceRequest(
                    priceDate,
                    ttfEurMwh: 30m,
                    version: 0,
                    egsiEtfEurMwh: 31m,
                    eurSek: 7.5m,
                    eurDkk: 7.46m
                ),
                actorId,
                default
            )
            .ConfigureAwait(false);
        Assert.NotNull(created);
        return created.Version;
    }

    private static async Task<long> UpdateAndAssertPartialMarketPriceAsync(
        MarketPriceRepository repository,
        DateOnly priceDate,
        Guid actorId,
        long createdVersion
    )
    {
        var updated = await repository
            .UpsertAtomicAsync(
                BuildMarketPriceRequest(priceDate, ttfEurMwh: 32m, version: createdVersion),
                actorId,
                default
            )
            .ConfigureAwait(false);
        Assert.NotNull(updated);
        Assert.Equal(32m, updated.TtfEurMwh);
        Assert.Equal(31m, updated.EgsiEtfEurMwh);
        Assert.Equal(7.5m, updated.EurSek);
        Assert.Equal(7.46m, updated.EurDkk);
        Assert.Equal(createdVersion + 1, updated.Version);
        return updated.Version;
    }

    private static async Task AssertRejectedMarketPriceUpsertsAsync(
        MarketPriceRepository repository,
        DateOnly priceDate,
        Guid actorId,
        long staleVersion
    )
    {
        Assert.Null(
            await repository
                .UpsertAtomicAsync(
                    BuildMarketPriceRequest(priceDate, ttfEurMwh: 99m, version: staleVersion),
                    actorId,
                    default
                )
                .ConfigureAwait(false)
        );
        Assert.Null(
            await repository
                .UpsertAtomicAsync(
                    BuildMarketPriceRequest(priceDate, ttfEurMwh: 99m, version: 0),
                    actorId,
                    default
                )
                .ConfigureAwait(false)
        );
    }

    private static UpsertMarketPriceRequest BuildMarketPriceRequest(
        DateOnly priceDate,
        decimal ttfEurMwh,
        long version,
        decimal? egsiEtfEurMwh = null,
        decimal? eurSek = null,
        decimal? eurDkk = null
    ) =>
        new(
            priceDate,
            ttfEurMwh,
            egsiEtfEurMwh,
            null,
            null,
            null,
            null,
            null,
            eurSek,
            null,
            null,
            null,
            eurDkk,
            version
        );

    private async Task AssertConcurrentDeleteCannotBeResurrectedAsync(
        MarketPriceRepository repository,
        DateOnly priceDate,
        Guid actorId,
        long version
    )
    {
        var deletingConnection = new NpgsqlConnection(Postgres.ConnectionString);
        await using (deletingConnection.ConfigureAwait(false))
        {
            await deletingConnection.OpenAsync().ConfigureAwait(false);
            var deletingTransaction = await deletingConnection
                .BeginTransactionAsync()
                .ConfigureAwait(false);
            await using (deletingTransaction.ConfigureAwait(false))
            {
                await deletingConnection
                    .ExecuteAsync(
                        "DELETE FROM market_prices WHERE price_date = @PriceDate AND version = @Version",
                        new { PriceDate = priceDate, Version = version },
                        deletingTransaction
                    )
                    .ConfigureAwait(false);

                var concurrentUpdate = repository.UpsertAtomicAsync(
                    BuildMarketPriceRequest(priceDate, ttfEurMwh: 33m, version: version),
                    actorId,
                    default
                );

                await WaitForMarketPriceLockWaitAsync().ConfigureAwait(false);
                await deletingTransaction.CommitAsync().ConfigureAwait(false);

                Assert.Null(await concurrentUpdate.ConfigureAwait(false));
                Assert.Null(
                    await repository.GetByDateAsync(priceDate, default).ConfigureAwait(false)
                );
            }
        }
    }

    private async Task<Guid> CreateCounterpartyAsync()
    {
        var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await using (connection.ConfigureAwait(false))
        {
            var id = Guid.NewGuid();
            await connection
                .ExecuteAsync(
                    "INSERT INTO counterparties (id, name, shorthand) VALUES (@Id, @Name, @Shorthand)",
                    new
                    {
                        Id = id,
                        Name = $"Repository Counterparty {id:N}",
                        Shorthand = $"RP{id:N}"[..20],
                    }
                )
                .ConfigureAwait(false);
            return id;
        }
    }

    private async Task WaitForMarketPriceLockWaitAsync()
    {
        var observer = new NpgsqlConnection(Postgres.ConnectionString);
        await using (observer.ConfigureAwait(false))
        {
            await observer.OpenAsync().ConfigureAwait(false);
            for (var attempt = 0; attempt < 100; attempt++)
            {
                var waiting = await observer
                    .ExecuteScalarAsync<bool>(
                        """
                        SELECT EXISTS (
                            SELECT 1
                            FROM pg_stat_activity
                            WHERE datname = current_database()
                              AND pid <> pg_backend_pid()
                              AND wait_event_type = 'Lock'
                              AND query ILIKE '%market_prices%')
                        """
                    )
                    .ConfigureAwait(false);
                if (waiting)
                {
                    return;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            throw new TimeoutException(
                "The market-price update did not reach the expected row-lock wait."
            );
        }
    }
}
