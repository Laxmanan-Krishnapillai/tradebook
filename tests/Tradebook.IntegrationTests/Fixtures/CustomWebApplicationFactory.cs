using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using Tradebook.Infrastructure.Data;
using Tradebook.Infrastructure.Migrations;

namespace Tradebook.IntegrationTests.Fixtures;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string JwtSigningKey = "integration-test-signing-key-32-bytes-6bf93fd240704c44";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .WithDatabase("tradebook_hermetic_test")
        .WithUsername("test_user")
        .WithPassword("test_password_123")
        .Build();

    private NpgsqlConnection _connection = null!;
    private Respawner _respawner = null!;

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);
        _connection = new NpgsqlConnection(ConnectionString);
        await _connection.OpenAsync().ConfigureAwait(false);

        MigrationRunner.Run(ConnectionString);

        _respawner = await Respawner
            .CreateAsync(
                _connection,
                new RespawnerOptions
                {
                    DbAdapter = DbAdapter.Postgres,
                    SchemasToInclude = ["public"],
                    TablesToIgnore = ["schema_journal"],
                }
            )
            .ConfigureAwait(false);
    }

    public Task ResetDatabaseAsync() => _respawner.ResetAsync(_connection);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // UseSetting lands in host configuration early enough for services-phase reads
        // (Wolverine envelope storage); the AddInMemoryCollection overrides below merge
        // later and only cover options-bound consumers.
        builder.UseSetting("Database:ConnectionString", ConnectionString);
        builder.ConfigureAppConfiguration(
            (_, configuration) =>
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        ["Database:ConnectionString"] = ConnectionString,
                        ["Entra:TenantId"] = "11111111-1111-1111-1111-111111111111",
                        ["Entra:ClientId"] = "22222222-2222-2222-2222-222222222222",
                    }
                )
        );
    }

    public override async ValueTask DisposeAsync()
    {
        // Stop the host first: Wolverine's node shutdown releases envelope ownership
        // against the database, so the container must still be running.
        await base.DisposeAsync().ConfigureAwait(false);
        await _connection.DisposeAsync().ConfigureAwait(false);
        await _container.DisposeAsync().ConfigureAwait(false);
    }
}
