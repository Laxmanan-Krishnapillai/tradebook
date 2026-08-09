using Npgsql;

namespace Tradebook.IntegrationTests;

public sealed class PostgresFixtureResetIntegrationTests(PostgresTestFixture postgres)
    : PostgresDatabaseTestBase(postgres)
{
    [Fact]
    public async Task ResetClearsApplicationRowsAndPreservesTheMigrationLedger()
    {
        var migrationCount = await CountAsync("SELECT count(*) FROM schema_migrations");
        Assert.True(migrationCount > 0);

        var counterpartyId = Guid.NewGuid();
        await using (var connection = new NpgsqlConnection(Postgres.ConnectionString))
        {
            await connection.OpenAsync();
            await using var insert = new NpgsqlCommand(
                "INSERT INTO counterparties (id, name, shorthand) VALUES (@id, @name, @shorthand)",
                connection
            );
            insert.Parameters.AddWithValue("id", counterpartyId);
            insert.Parameters.AddWithValue(
                "name",
                $"Fixture reset verification {counterpartyId:N}"
            );
            insert.Parameters.AddWithValue("shorthand", $"PGR{counterpartyId:N}"[..20]);
            Assert.Equal(1, await insert.ExecuteNonQueryAsync());
        }

        Assert.Equal(
            1,
            await CountAsync("SELECT count(*) FROM counterparties WHERE id = @id", counterpartyId)
        );

        await Postgres.ResetDatabaseAsync();

        Assert.Equal(
            0,
            await CountAsync("SELECT count(*) FROM counterparties WHERE id = @id", counterpartyId)
        );
        Assert.Equal(migrationCount, await CountAsync("SELECT count(*) FROM schema_migrations"));
    }

    private async Task<long> CountAsync(string sql, Guid? id = null)
    {
        var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            var command = new NpgsqlCommand(sql, connection);
            await using (command.ConfigureAwait(false))
            {
                if (id is not null)
                {
                    command.Parameters.AddWithValue("id", id.Value);
                }

                return (long)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
            }
        }
    }
}
