using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using Tradebook.Core.Domain;
using Tradebook.Core.DTOs;
using Tradebook.Infrastructure.Data;
using Tradebook.Infrastructure.Options;

namespace Tradebook.IntegrationTests;

public sealed class DomainRepositoryIntegrationTests(PostgresTestFixture postgres) : PostgresDatabaseTestBase(postgres)
{
    [Fact]
    public async Task Every_task02_repository_writes_a_real_transactional_outbox_event()
    {
        var actorId = Guid.NewGuid();
        var counterpartyId = await CreateCounterpartyAsync();
        await using var connections = new NpgsqlConnectionFactory(Options.Create(
            new DatabaseOptions { ConnectionString = Postgres.ConnectionString }));

        var contract = await new ContractRepository(connections).CreateAtomicAsync(
            new CreateContractRequest(
                $"REPO45.SG.{Guid.NewGuid():N}.NOQS", counterpartyId, "Gas", "Sell",
                "REPO", "DK", 45, null, 2026, null, null, "BGEM", null, "SUB",
                "TTF", null, "External", "repository integration"),
            actorId, default);

        var capacity = await new CapacityBookingRepository(connections).CreateAtomicAsync(
            new CreateCapacityBookingRequest(
                contract.ContractId, new DateOnly(2026, 1, 1), null, counterpartyId,
                "BGEM", null, "GTF", "THE", null, null, null, null, 10m, 2m, 20m, null),
            actorId, default);
        var transfer = await new TransferRepository(connections).CreateAtomicAsync(
            new CreateTransferRequest(
                contract.ContractId, new DateOnly(2026, 2, 1), null, counterpartyId,
                "BGEM", "GTF", 10m, 9m, 100m, 1m, null, null, "TTF", 2m, 1m,
                "Awaiting", null),
            actorId, default);
        var bioticket = await new BioticketRepository(connections).CreateAtomicAsync(
            new CreateBioticketRequest(
                contract.ContractId, "Sales", new DateOnly(2026, 3, 1), null, null, null,
                10m, 9m, 9m, 15m, 135m, .25m, 33.75m, 168.75m, "Awaiting", null),
            actorId, default);
        var goo = await new GooCertificateRepository(connections).CreateAtomicAsync(
            new CreateGooCertificateTransactionRequest(
                $"SF-{Guid.NewGuid():N}", "Repository event", null, null, "DK",
                contract.ContractId, "Producer", 1m, new DateOnly(2026, 3, 1),
                null, null, "DENA", "Latest transaction", new DateOnly(2026, 3, 1),
                9m, 9m, "Biogas", null),
            actorId, default);
        var market = await new MarketPriceRepository(connections).UpsertAtomicAsync(
            new UpsertMarketPriceRequest(
                new DateOnly(2026, 4, 1), 30m, null, null, null, null, null, null,
                7.5m, null, null, null, 7.46m, 0),
            actorId, default);
        var tax = await new TaxTariffRepository(connections).CreateAtomicAsync(
            new CreateTaxTariffRequest(
                contract.ContractId, counterpartyId, new DateOnly(2026, 5, 1),
                new DateOnly(2026, 5, 31), 1m, 2m, 3m, 4m, 5m, 6m, "SEK"),
            actorId, default);
        var hedgeRepository = new HedgeRepository(connections);
        var hedge = await hedgeRepository.CreateAtomicAsync(
            new CreateHedgeRequest(contract.ContractId, new DateOnly(2026, 6, 1), 100m, 29m),
            actorId, default);

        Assert.Equal($"{contract.ContractName}-1-2026", capacity.ContractInstanceId);
        Assert.Equal($"{contract.ContractName}-2-2026", transfer.ContractInstanceId);
        Assert.Equal($"{contract.ContractName}-3-2026", bioticket.ContractInstanceId);
        Assert.NotNull(market);

        var updatedHedge = await hedgeRepository.UpdateAtomicAsync(
            new UpdateHedgeRequest(hedge.HedgeId, 110m, 30m, hedge.Version), actorId, default);
        Assert.NotNull(updatedHedge);
        Assert.Null(await hedgeRepository.UpdateAtomicAsync(
            new UpdateHedgeRequest(hedge.HedgeId, 120m, 31m, hedge.Version), actorId, default));

        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        var aggregateTypes = (await connection.QueryAsync<string>(
            """
            SELECT DISTINCT aggregate_type
            FROM outbox_events
            WHERE aggregate_type = ANY(@Expected)
            """, new
            {
                Expected = new[]
                {
                    OutboxAggregateTypes.Contract,
                    OutboxAggregateTypes.CapacityBooking,
                    OutboxAggregateTypes.Transfer,
                    OutboxAggregateTypes.BioticketDelivery,
                    OutboxAggregateTypes.GooCertificateTransaction,
                    OutboxAggregateTypes.MarketPrice,
                    OutboxAggregateTypes.TaxTariff,
                    OutboxAggregateTypes.Hedge
                }
            })).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(8, aggregateTypes.Count);
        Assert.Contains(OutboxAggregateTypes.Contract, aggregateTypes);
        Assert.Contains(OutboxAggregateTypes.CapacityBooking, aggregateTypes);
        Assert.Contains(OutboxAggregateTypes.Transfer, aggregateTypes);
        Assert.Contains(OutboxAggregateTypes.BioticketDelivery, aggregateTypes);
        Assert.Contains(OutboxAggregateTypes.GooCertificateTransaction, aggregateTypes);
        Assert.Contains(OutboxAggregateTypes.MarketPrice, aggregateTypes);
        Assert.Contains(OutboxAggregateTypes.TaxTariff, aggregateTypes);
        Assert.Contains(OutboxAggregateTypes.Hedge, aggregateTypes);

        var auditedActors = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM audit_log WHERE actor_id = @ActorId", new { ActorId = actorId });
        Assert.True(auditedActors >= 9);

        Assert.NotEqual(Guid.Empty, goo.GooCertificateTransactionId.Value);
        Assert.NotEqual(Guid.Empty, tax.TaxTariffId.Value);
    }

    [Fact]
    public async Task Market_price_updates_are_partial_versioned_and_cannot_resurrect_a_concurrent_delete()
    {
        var actorId = Guid.NewGuid();
        var priceDate = new DateOnly(2041, 7, 19);
        await using var connections = new NpgsqlConnectionFactory(Options.Create(
            new DatabaseOptions { ConnectionString = Postgres.ConnectionString }));
        var repository = new MarketPriceRepository(connections);

        var created = await repository.UpsertAtomicAsync(
            new UpsertMarketPriceRequest(
                priceDate, 30m, 31m, null, null, null, null, null,
                7.5m, null, null, null, 7.46m, 0),
            actorId,
            default);
        Assert.NotNull(created);

        var updated = await repository.UpsertAtomicAsync(
            new UpsertMarketPriceRequest(
                priceDate, 32m, null, null, null, null, null, null,
                null, null, null, null, null, created.Version),
            actorId,
            default);
        Assert.NotNull(updated);
        Assert.Equal(32m, updated.TtfEurMwh);
        Assert.Equal(31m, updated.EgsiEtfEurMwh);
        Assert.Equal(7.5m, updated.EurSek);
        Assert.Equal(7.46m, updated.EurDkk);
        Assert.Equal(created.Version + 1, updated.Version);

        Assert.Null(await repository.UpsertAtomicAsync(
            new UpsertMarketPriceRequest(
                priceDate, 99m, null, null, null, null, null, null,
                null, null, null, null, null, created.Version),
            actorId,
            default));
        Assert.Null(await repository.UpsertAtomicAsync(
            new UpsertMarketPriceRequest(
                priceDate, 99m, null, null, null, null, null, null,
                null, null, null, null, null, 0),
            actorId,
            default));

        // Hold an uncommitted delete so the versioned update is forced to wait on
        // the row lock. Once the delete commits, the update must return a conflict;
        // an INSERT/ON CONFLICT implementation can otherwise recreate the row.
        await using var deletingConnection = new NpgsqlConnection(Postgres.ConnectionString);
        await deletingConnection.OpenAsync();
        await using var deletingTransaction = await deletingConnection.BeginTransactionAsync();
        await deletingConnection.ExecuteAsync(
            "DELETE FROM market_prices WHERE price_date = @PriceDate AND version = @Version",
            new { PriceDate = priceDate, updated.Version },
            deletingTransaction);

        var concurrentUpdate = repository.UpsertAtomicAsync(
            new UpsertMarketPriceRequest(
                priceDate, 33m, null, null, null, null, null, null,
                null, null, null, null, null, updated.Version),
            actorId,
            default);

        await WaitForMarketPriceLockWaitAsync();
        await deletingTransaction.CommitAsync();

        Assert.Null(await concurrentUpdate);
        Assert.Null(await repository.GetByDateAsync(priceDate, default));
    }

    private async Task<Guid> CreateCounterpartyAsync()
    {
        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO counterparties (id, name, shorthand) VALUES (@Id, @Name, @Shorthand)",
            new { Id = id, Name = $"Repository Counterparty {id:N}", Shorthand = $"RP{id:N}"[..20] });
        return id;
    }

    private async Task WaitForMarketPriceLockWaitAsync()
    {
        await using var observer = new NpgsqlConnection(Postgres.ConnectionString);
        await observer.OpenAsync();
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var waiting = await observer.ExecuteScalarAsync<bool>(
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM pg_stat_activity
                    WHERE datname = current_database()
                      AND pid <> pg_backend_pid()
                      AND wait_event_type = 'Lock'
                      AND query ILIKE '%market_prices%')
                """);
            if (waiting)
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("The market-price update did not reach the expected row-lock wait.");
    }
}
