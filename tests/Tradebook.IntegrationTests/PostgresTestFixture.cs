using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using Tradebook.Infrastructure.Data;
using Tradebook.Infrastructure.Migrations;

namespace Tradebook.IntegrationTests;

public sealed class PostgresTestFixture : IAsyncLifetime
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
        await _container.StartAsync();
        MigrationRunner.Run(ConnectionString);

        _connection = new NpgsqlConnection(ConnectionString);
        await _connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = ["schema_journal"]
        });
    }

    public Task ResetDatabaseAsync() => _respawner.ResetAsync(_connection);

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _container.DisposeAsync();
    }

}

public abstract class PostgresDatabaseTestBase(PostgresTestFixture fixture)
    : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    protected PostgresTestFixture Postgres { get; } = fixture;

    protected virtual bool ResetDatabaseBeforeEachTest => true;

    public virtual Task InitializeAsync() =>
        ResetDatabaseBeforeEachTest ? Postgres.ResetDatabaseAsync() : Task.CompletedTask;

    public virtual Task DisposeAsync() => Task.CompletedTask;

}
