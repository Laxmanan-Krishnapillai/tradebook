using Dapper;
using Npgsql;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using Tradebook.Infrastructure.Data;

namespace Tradebook.IntegrationTests;

public sealed class VogenDapperRoundTripTests(PostgresTestFixture postgres) : PostgresDatabaseTestBase(postgres)
{
    [Fact]
    public async Task Vogen_values_round_trip_through_postgresql_17_without_manual_unwrapping()
    {
        VogenTypeHandlers.RegisterAll();
        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await connection.OpenAsync();
        var expected = new DomainPrimitiveRow(
            ContractId.New(), Price.From(12.3456m), Quantity.From(-12.34567890m), Amount.From(-98.7654m));

        var actual = await connection.QuerySingleAsync<DomainPrimitiveRow>(
            "SELECT @Id::uuid AS Id, @Price::numeric AS Price, @Quantity::numeric AS Quantity, @Amount::numeric AS Amount",
            expected);

        Assert.Equal(expected, actual);
    }

    private sealed record DomainPrimitiveRow(ContractId Id, Price Price, Quantity Quantity, Amount Amount);
}
