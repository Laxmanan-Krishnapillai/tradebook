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

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connection = new NpgsqlConnection(ConnectionString);
        await _connection.OpenAsync();

        MigrationRunner.Run(ConnectionString);

        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = ["schema_journal"]
        });
    }

    public Task ResetDatabaseAsync() => _respawner.ResetAsync(_connection);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = ConnectionString,
                ["Jwt:Issuer"] = "Tradebook",
                ["Jwt:Audience"] = "Tradebook",
                ["Jwt:SigningKey"] = JwtSigningKey
            }));
    }

    public override async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

}
