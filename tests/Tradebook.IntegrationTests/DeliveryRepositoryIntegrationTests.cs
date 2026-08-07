using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;
using Tradebook.Infrastructure.Data;
using Tradebook.Infrastructure.Options;

namespace Tradebook.IntegrationTests;

public sealed class DeliveryRepositoryIntegrationTests(PostgresTestFixture postgres) : PostgresDatabaseTestBase(postgres)
{
    [Fact]
    public async Task Mutations_write_audit_and_outbox_and_enforce_versions()
    {
        var actorId = Guid.NewGuid();
        var (contractId, contractName) = await CreateContractAsync();
        await using var factory = new NpgsqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = Postgres.ConnectionString }));
        var repository = new DeliveryRepository(factory);
        var created = await repository.CreateAtomicAsync(new CreatePhysicalDeliveryRequest(contractId, null, "Sales", new DateOnly(2026, 1, 1), null, 10m, 9m, "TTF", null, null), actorId, CancellationToken.None);
        Assert.Equal($"{contractName}-1-2026", created.ContractInstanceId);

        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        var auditActor = await connection.ExecuteScalarAsync<Guid>("SELECT actor_id FROM audit_log WHERE entity_name = 'physical_deliveries' AND entity_id = @Id", new { Id = created.DeliveryId.ToString() });
        var outboxCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM outbox_events WHERE aggregate_type = 'PhysicalDelivery' AND aggregate_id = @Id", new { Id = created.DeliveryId.ToString() });
        Assert.Equal(actorId, auditActor);
        Assert.Equal(1, outboxCount);

        var updated = await repository.UpdateAtomicAsync(new UpdatePhysicalDeliveryRequest(created.DeliveryId, 11m, null, created.Version), actorId, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal(created.Version + 1, updated.Version);
        var stale = await repository.UpdateAtomicAsync(new UpdatePhysicalDeliveryRequest(created.DeliveryId, 12m, null, created.Version), actorId, CancellationToken.None);
        Assert.Null(stale);

        var cancelled = await repository.CancelAtomicAsync(created.DeliveryId, updated.Version, "Duplicate", actorId, CancellationToken.None);
        Assert.Null(cancelled);
        var current = await repository.GetByIdAsync(created.DeliveryId, CancellationToken.None);
        Assert.Equal("Cancelled", current!.Status);
    }

    private async Task<(Guid Id, string Name)> CreateContractAsync()
    {
        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await connection.OpenAsync();
        var counterpartyId = Guid.NewGuid();
        await connection.ExecuteAsync("INSERT INTO counterparties (id, name, shorthand) VALUES (@Id, @Name, @Shorthand)", new { Id = counterpartyId, Name = $"Counterparty-{counterpartyId}", Shorthand = $"CP{counterpartyId:N}"[..20] });
        var contractId = Guid.NewGuid();
        var contractName = $"TEST45.SG.{contractId:N}";
        await connection.ExecuteAsync("INSERT INTO contracts (id, contract_name, counterparty_id, product_type, action) VALUES (@Id, @Name, @CounterpartyId, 'Gas', 'Sell')", new { Id = contractId, Name = contractName, CounterpartyId = counterpartyId });
        return (contractId, contractName);
    }
}
