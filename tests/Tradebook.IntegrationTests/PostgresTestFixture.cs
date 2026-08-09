using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using Tradebook.Infrastructure.Data;
using Tradebook.Infrastructure.Migrations;
using Tradebook.Infrastructure.Options;

namespace Tradebook.IntegrationTests;

public sealed class PostgresTestFixture : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .WithDatabase("tradebook_test")
        .WithUsername("tradebook")
        .WithPassword("tradebook")
        .Build();

    private NpgsqlConnection _connection = null!;
    private Respawner _respawner = null!;

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);
        var connections = new NpgsqlConnectionFactory(
            Options.Create(new DatabaseOptions { ConnectionString = ConnectionString })
        );
        await using (connections.ConfigureAwait(false))
        {
            await new DatabaseMigrator(connections, NullLogger<DatabaseMigrator>.Instance)
                .MigrateAsync()
                .ConfigureAwait(false);

            _connection = new NpgsqlConnection(ConnectionString);
            await _connection.OpenAsync().ConfigureAwait(false);
            _respawner = await Respawner
                .CreateAsync(
                    _connection,
                    new RespawnerOptions
                    {
                        DbAdapter = DbAdapter.Postgres,
                        SchemasToInclude = ["public"],
                        TablesToIgnore = ["schema_migrations"],
                    }
                )
                .ConfigureAwait(false);
        }
    }

    public Task ResetDatabaseAsync() => _respawner.ResetAsync(_connection);

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync().ConfigureAwait(false);
        await _container.DisposeAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        _connection?.Dispose();
        GC.SuppressFinalize(this);
    }
}
