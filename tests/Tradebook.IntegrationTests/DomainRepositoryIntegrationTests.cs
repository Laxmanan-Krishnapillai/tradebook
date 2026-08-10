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
    [Fact]
    public async Task EveryTask02RepositoryPublishesATransactionalDomainEvent()
    {
        var actorId = Guid.NewGuid();
        var counterpartyId = await CreateCounterpartyAsync().ConfigureAwait(true);
        var connections = new NpgsqlConnectionFactory(
            Options.Create(new DatabaseOptions { ConnectionString = Postgres.ConnectionString })
        );
        await using var configuredConnections = connections.ConfigureAwait(true);
        var publisher = new RecordingTransactionalEventPublisher();

        var contract = await CreateContractRecordAsync(
                connections,
                publisher,
                counterpartyId,
                actorId
            )
            .ConfigureAwait(true);
        var capacity = await CreateCapacityBookingAsync(
                connections,
                publisher,
                contract.ContractId,
                counterpartyId,
                actorId
            )
            .ConfigureAwait(true);
        var transfer = await CreateTransferAsync(
                connections,
                publisher,
                contract.ContractId,
                counterpartyId,
                actorId
            )
            .ConfigureAwait(true);
        var bioticket = await CreateBioticketAsync(
                connections,
                publisher,
                contract.ContractId,
                actorId
            )
            .ConfigureAwait(true);
        var (goo, market, tax) = await CreateOtherRecordsAsync(
                connections,
                publisher,
                contract.ContractId,
                actorId,
                counterpartyId
            )
            .ConfigureAwait(true);

        Assert.Equal($"{contract.ContractName}-1-2026", capacity.ContractInstanceId);
        Assert.Equal($"{contract.ContractName}-2-2026", transfer.ContractInstanceId);
        Assert.Equal($"{contract.ContractName}-3-2026", bioticket.ContractInstanceId);
        Assert.NotNull(market);

        await VerifyHedgeVersioningAsync(connections, publisher, contract, actorId)
            .ConfigureAwait(true);
        await VerifyPublishedEventsAsync(actorId, publisher, goo, tax).ConfigureAwait(true);
    }

    private static async Task<(
        GooCertificateTransactionDetailsDto Goo,
        MarketPriceDetailsDto? Market,
        TaxTariffDetailsDto Tax
    )> CreateOtherRecordsAsync(
        NpgsqlConnectionFactory connections,
        RecordingTransactionalEventPublisher publisher,
        Guid contractId,
        Guid actorId,
        Guid counterpartyId
    )
    {
        var goo = await CreateGooCertificateAsync(connections, publisher, contractId, actorId)
            .ConfigureAwait(false);
        var market = await CreateMarketPriceAsync(connections, publisher, actorId)
            .ConfigureAwait(false);
        var tax = await CreateTaxTariffAsync(
                connections,
                publisher,
                contractId,
                counterpartyId,
                actorId
            )
            .ConfigureAwait(false);
        return (goo, market, tax);
    }

    private static async Task VerifyHedgeVersioningAsync(
        NpgsqlConnectionFactory connections,
        RecordingTransactionalEventPublisher publisher,
        ContractDetailsDto contract,
        Guid actorId
    )
    {
        var repository = new HedgeRepository(connections, publisher);
        var hedge = await repository
            .CreateAtomicAsync(
                new CreateHedgeRequest(contract.ContractId, new DateOnly(2026, 6, 1), 100m, 29m),
                actorId,
                default
            )
            .ConfigureAwait(false);
        var updatedHedge = await repository
            .UpdateAtomicAsync(
                new UpdateHedgeRequest(hedge.HedgeId, 110m, 30m, hedge.Version),
                actorId,
                default
            )
            .ConfigureAwait(false);
        Assert.NotNull(updatedHedge);
        Assert.Null(
            await repository
                .UpdateAtomicAsync(
                    new UpdateHedgeRequest(hedge.HedgeId, 120m, 31m, hedge.Version),
                    actorId,
                    default
                )
                .ConfigureAwait(false)
        );
    }

    private static Task<ContractDetailsDto> CreateContractRecordAsync(
        NpgsqlConnectionFactory connections,
        RecordingTransactionalEventPublisher publisher,
        Guid counterpartyId,
        Guid actorId
    ) =>
        new ContractRepository(connections, publisher).CreateAtomicAsync(
            new CreateContractRequest(
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
            ),
            actorId,
            default
        );

    private static Task<CapacityBookingDetailsDto> CreateCapacityBookingAsync(
        NpgsqlConnectionFactory connections,
        RecordingTransactionalEventPublisher publisher,
        Guid contractId,
        Guid counterpartyId,
        Guid actorId
    ) =>
        new CapacityBookingRepository(connections, publisher).CreateAtomicAsync(
            new CreateCapacityBookingRequest(
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
            ),
            actorId,
            default
        );

    private static Task<TransferDetailsDto> CreateTransferAsync(
        NpgsqlConnectionFactory connections,
        RecordingTransactionalEventPublisher publisher,
        Guid contractId,
        Guid counterpartyId,
        Guid actorId
    ) =>
        new TransferRepository(connections, publisher).CreateAtomicAsync(
            new CreateTransferRequest(
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
            ),
            actorId,
            default
        );

    private static Task<BioticketDetailsDto> CreateBioticketAsync(
        NpgsqlConnectionFactory connections,
        RecordingTransactionalEventPublisher publisher,
        Guid contractId,
        Guid actorId
    ) =>
        new BioticketRepository(connections, publisher).CreateAtomicAsync(
            new CreateBioticketRequest(
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
            ),
            actorId,
            default
        );

    private static Task<GooCertificateTransactionDetailsDto> CreateGooCertificateAsync(
        NpgsqlConnectionFactory connections,
        RecordingTransactionalEventPublisher publisher,
        Guid contractId,
        Guid actorId
    ) =>
        new GooCertificateRepository(connections, publisher).CreateAtomicAsync(
            new CreateGooCertificateTransactionRequest(
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
            ),
            actorId,
            default
        );

    private static Task<MarketPriceDetailsDto?> CreateMarketPriceAsync(
        NpgsqlConnectionFactory connections,
        RecordingTransactionalEventPublisher publisher,
        Guid actorId
    ) =>
        new MarketPriceRepository(connections, publisher).UpsertAtomicAsync(
            new UpsertMarketPriceRequest(
                new DateOnly(2026, 4, 1),
                30m,
                null,
                null,
                null,
                null,
                null,
                null,
                7.5m,
                null,
                null,
                null,
                7.46m,
                0
            ),
            actorId,
            default
        );

    private static Task<TaxTariffDetailsDto> CreateTaxTariffAsync(
        NpgsqlConnectionFactory connections,
        RecordingTransactionalEventPublisher publisher,
        Guid contractId,
        Guid counterpartyId,
        Guid actorId
    ) =>
        new TaxTariffRepository(connections, publisher).CreateAtomicAsync(
            new CreateTaxTariffRequest(
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
            ),
            actorId,
            default
        );

    private async Task VerifyPublishedEventsAsync(
        Guid actorId,
        RecordingTransactionalEventPublisher publisher,
        GooCertificateTransactionDetailsDto goo,
        TaxTariffDetailsDto tax
    )
    {
        var aggregateTypes = publisher
            .Events.Select(item => item.AggregateType)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(8, aggregateTypes.Count);
        Assert.Contains(RealtimeAggregateTypes.Contract, aggregateTypes);
        Assert.Contains(RealtimeAggregateTypes.CapacityBooking, aggregateTypes);
        Assert.Contains(RealtimeAggregateTypes.Transfer, aggregateTypes);
        Assert.Contains(RealtimeAggregateTypes.BioticketDelivery, aggregateTypes);
        Assert.Contains(RealtimeAggregateTypes.GooCertificateTransaction, aggregateTypes);
        Assert.Contains(RealtimeAggregateTypes.MarketPrice, aggregateTypes);
        Assert.Contains(RealtimeAggregateTypes.TaxTariff, aggregateTypes);
        Assert.Contains(RealtimeAggregateTypes.Hedge, aggregateTypes);
        Assert.Equal(publisher.Events.Count, publisher.Transactions.Count);
        Assert.Equal(publisher.Events.Count, publisher.FlushCount);

        var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await using var configuredConnection = connection.ConfigureAwait(false);
        var auditedActors = await connection
            .ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM audit_log WHERE actor_id = @ActorId",
                new { ActorId = actorId }
            )
            .ConfigureAwait(false);
        Assert.True(auditedActors >= 9);
        Assert.NotEqual(Guid.Empty, goo.GooCertificateTransactionId.Value);
        Assert.NotEqual(Guid.Empty, tax.TaxTariffId.Value);
    }

    [Fact]
    public async Task MarketPriceUpdatesArePartialVersionedAndCannotResurrectAConcurrentDelete()
    {
        var actorId = Guid.NewGuid();
        var priceDate = new DateOnly(2041, 7, 19);
        var connections = new NpgsqlConnectionFactory(
            Options.Create(new DatabaseOptions { ConnectionString = Postgres.ConnectionString })
        );
        await using var configuredConnections = connections.ConfigureAwait(true);
        var publisher = new RecordingTransactionalEventPublisher();
        var repository = new MarketPriceRepository(connections, publisher);

        var created = await repository
            .UpsertAtomicAsync(
                CreateMarketPriceRequest(priceDate, 30m, 31m, 7.5m, 7.46m, 0),
                actorId,
                default
            )
            .ConfigureAwait(true);
        Assert.NotNull(created);

        var updated = await repository
            .UpsertAtomicAsync(
                CreateMarketPriceRequest(priceDate, 32m, null, null, null, created.Version),
                actorId,
                default
            )
            .ConfigureAwait(true);
        Assert.NotNull(updated);
        Assert.Equal(32m, updated.TtfEurMwh);
        Assert.Equal(31m, updated.EgsiEtfEurMwh);
        Assert.Equal(7.5m, updated.EurSek);
        Assert.Equal(7.46m, updated.EurDkk);
        Assert.Equal(created.Version + 1, updated.Version);

        Assert.Null(
            await repository
                .UpsertAtomicAsync(
                    CreateMarketPriceRequest(priceDate, 99m, null, null, null, created.Version),
                    actorId,
                    default
                )
                .ConfigureAwait(true)
        );
        Assert.Null(
            await repository
                .UpsertAtomicAsync(
                    CreateMarketPriceRequest(priceDate, 99m, null, null, null, 0),
                    actorId,
                    default
                )
                .ConfigureAwait(true)
        );
        await VerifyConcurrentDeleteDoesNotResurrectAsync(repository, priceDate, updated, actorId)
            .ConfigureAwait(true);
        Assert.Equal(2, publisher.Events.Count);
        Assert.Equal(2, publisher.FlushCount);
    }

    private static UpsertMarketPriceRequest CreateMarketPriceRequest(
        DateOnly priceDate,
        decimal? ttfEurMwh,
        decimal? egsiEtfEurMwh,
        decimal? eurSek,
        decimal? eurDkk,
        long version
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

    private async Task VerifyConcurrentDeleteDoesNotResurrectAsync(
        MarketPriceRepository repository,
        DateOnly priceDate,
        MarketPriceDetailsDto updated,
        Guid actorId
    )
    {
        // Hold an uncommitted delete so the versioned update is forced to wait on
        // the row lock. Once the delete commits, the update must return a conflict;
        // an INSERT/ON CONFLICT implementation can otherwise recreate the row.
        var deletingConnection = new NpgsqlConnection(Postgres.ConnectionString);
        await using var configuredConnection = deletingConnection.ConfigureAwait(false);
        await deletingConnection.OpenAsync().ConfigureAwait(false);
        var deletingTransaction = await deletingConnection
            .BeginTransactionAsync()
            .ConfigureAwait(false);
        await using var configuredTransaction = deletingTransaction.ConfigureAwait(false);
        await deletingConnection
            .ExecuteAsync(
                "DELETE FROM market_prices WHERE price_date = @PriceDate AND version = @Version",
                new { PriceDate = priceDate, updated.Version },
                deletingTransaction
            )
            .ConfigureAwait(false);

        var concurrentUpdate = repository.UpsertAtomicAsync(
            CreateMarketPriceRequest(priceDate, 33m, null, null, null, updated.Version),
            actorId,
            default
        );
        await WaitForMarketPriceLockWaitAsync().ConfigureAwait(false);
        await deletingTransaction.CommitAsync().ConfigureAwait(false);
        Assert.Null(await concurrentUpdate.ConfigureAwait(false));
        Assert.Null(await repository.GetByDateAsync(priceDate, default).ConfigureAwait(false));
    }

    private async Task<Guid> CreateCounterpartyAsync()
    {
        var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await using var configuredConnection = connection.ConfigureAwait(false);
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

    private async Task WaitForMarketPriceLockWaitAsync()
    {
        var observer = new NpgsqlConnection(Postgres.ConnectionString);
        await using var configuredObserver = observer.ConfigureAwait(false);
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
