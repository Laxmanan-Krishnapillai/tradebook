using Dapper;
using Npgsql;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using Tradebook.Infrastructure.Data.Generated;

namespace Tradebook.IntegrationTests;

public sealed class GeneratedDataAccessIntegrationTests(PostgresTestFixture postgres)
    : PostgresDatabaseTestBase(postgres)
{
    [Fact]
    public async Task Generated_query_maps_contract_id_and_price_value_objects()
    {
        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await connection.OpenAsync();
        var counterpartyId = Guid.NewGuid();
        var contractId = ContractId.New();
        await connection.ExecuteAsync("""
            INSERT INTO counterparties (id, name) VALUES (@CounterpartyId, 'sqlc counterparty');
            INSERT INTO contracts (
                id, contract_name, counterparty_id, product_type, action,
                fixed_price_gas_eur_mwh)
            VALUES (@ContractId, 'sqlc contract', @CounterpartyId, 'Gas', 'Purchase', 42.125);
            """, new { CounterpartyId = counterpartyId, ContractId = contractId.Value });

        using var queries = new ContractsSql(Postgres.ConnectionString);
        var result = await queries.GetContractPersistenceProbeAsync(
            new ContractsSql.GetContractPersistenceProbeArgs { Id = contractId });

        Assert.NotNull(result);
        Assert.Equal(contractId, result.Id);
        Assert.Equal(Price.From(42.125m), result.Price);
    }
}
