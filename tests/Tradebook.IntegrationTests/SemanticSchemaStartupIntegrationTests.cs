using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Tradebook.IntegrationTests.Fixtures;

namespace Tradebook.IntegrationTests;

public sealed class SemanticSchemaStartupIntegrationTests(PostgresTestFixture postgres)
    : PostgresDatabaseTestBase(postgres)
{
    [Fact]
    public void HostStartsWhenTheSemanticModelMatchesTheDatabaseSchema()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        Assert.NotNull(client);
    }

    [Fact]
    public async Task HostStopsWhenADeclaredSemanticColumnIsMissing()
    {
        // Schema validation moved out of synchronous startup into MigrationHostedService:
        // the host boots (liveness stays database-independent), the background migration
        // pass detects the drift and requests an application stop instead of throwing.
        await RenameVolumeColumnAsync("volume_mwh", "volume_mwh_drifted");
        try
        {
            using var factory = CreateFactory();
            using var client = factory.CreateClient();

            var lifetime = factory.Services.GetRequiredService<IHostApplicationLifetime>();
            var stopRequested = new TaskCompletionSource();
            using var stopRegistration = lifetime.ApplicationStopping.Register(() =>
                stopRequested.TrySetResult()
            );

            var completed = await Task.WhenAny(
                stopRequested.Task,
                Task.Delay(TimeSpan.FromSeconds(60))
            );

            Assert.Same(stopRequested.Task, completed);
        }
        finally
        {
            await RenameVolumeColumnAsync("volume_mwh_drifted", "volume_mwh");
        }
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder
                .UseEnvironment("Testing")
                // UseSetting lands early enough for services-phase reads (Wolverine
                // envelope storage); the in-memory overrides below cover options-bound
                // consumers.
                .UseSetting("Database:ConnectionString", Postgres.ConnectionString)
                .ConfigureAppConfiguration(
                    (_, configuration) =>
                        configuration.AddInMemoryCollection(
                            new Dictionary<string, string?>(StringComparer.Ordinal)
                            {
                                ["Database:ConnectionString"] = Postgres.ConnectionString,
                                ["Entra:TenantId"] = "11111111-1111-1111-1111-111111111111",
                                ["Entra:ClientId"] = "22222222-2222-2222-2222-222222222222",
                            }
                        )
                )
        );

    private async Task RenameVolumeColumnAsync(string from, string to)
    {
        var sql = (from, to) switch
        {
            ("volume_mwh", "volume_mwh_drifted") =>
                "ALTER TABLE physical_deliveries RENAME COLUMN volume_mwh TO volume_mwh_drifted",
            ("volume_mwh_drifted", "volume_mwh") =>
                "ALTER TABLE physical_deliveries RENAME COLUMN volume_mwh_drifted TO volume_mwh",
            _ => throw new ArgumentOutOfRangeException(
                nameof(from),
                "Unrecognized test column rename."
            ),
        };
        var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await using var configuredConnection = connection.ConfigureAwait(false);
        await connection.OpenAsync().ConfigureAwait(false);
        var command = new NpgsqlCommand(sql, connection);
        await using var configuredCommand = command.ConfigureAwait(false);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
