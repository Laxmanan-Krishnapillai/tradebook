using Npgsql;
using Tradebook.IntegrationTests.Fixtures;

namespace Tradebook.IntegrationTests;

public sealed class DatabaseResetIntegrationTests(CustomWebApplicationFactory factory)
    : DatabaseTestBase(factory)
{
    [Fact]
    public async Task RespawnResetClearsApplicationRowsAndPreservesTheMigrationLedger()
    {
        var beforeReset = await CountAppliedMigrationsAsync();
        Assert.True(beforeReset > 0);

        var counterpartyId = Guid.NewGuid();
        await using (var connection = new NpgsqlConnection(Factory.ConnectionString))
        {
            await connection.OpenAsync();
            await using var insert = new NpgsqlCommand(
                "INSERT INTO counterparties (id, name, shorthand) VALUES (@id, @name, @shorthand)",
                connection
            );
            insert.Parameters.AddWithValue("id", counterpartyId);
            insert.Parameters.AddWithValue("name", $"Reset verification {counterpartyId:N}");
            insert.Parameters.AddWithValue("shorthand", $"RST{counterpartyId:N}"[..20]);
            Assert.Equal(1, await insert.ExecuteNonQueryAsync());
        }

        Assert.Equal(1, await CountCounterpartyAsync(counterpartyId));

        await Factory.ResetDatabaseAsync();

        Assert.Equal(0, await CountCounterpartyAsync(counterpartyId));
        Assert.Equal(beforeReset, await CountAppliedMigrationsAsync());
    }

    private async Task<long> CountCounterpartyAsync(Guid counterpartyId)
    {
        var connection = new NpgsqlConnection(Factory.ConnectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            var command = new NpgsqlCommand(
                "SELECT count(*) FROM counterparties WHERE id = @id",
                connection
            );
            await using (command.ConfigureAwait(false))
            {
                command.Parameters.AddWithValue("id", counterpartyId);
                return (long)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
            }
        }
    }

    private async Task<long> CountAppliedMigrationsAsync()
    {
        var connection = new NpgsqlConnection(Factory.ConnectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            var command = new NpgsqlCommand("SELECT count(*) FROM schema_migrations", connection);
            await using (command.ConfigureAwait(false))
            {
                return (long)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
            }
        }
    }
}
