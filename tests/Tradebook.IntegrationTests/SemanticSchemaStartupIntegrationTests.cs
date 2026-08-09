using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Tradebook.Core.Analytics;
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
    public async Task HostStartupFailsWhenADeclaredSemanticColumnIsMissing()
    {
        await RenameVolumeColumnAsync("volume_mwh", "volume_mwh_drifted");
        try
        {
            using var factory = CreateFactory();

            var exception = Assert.Throws<SemanticSchemaMismatchException>(factory.CreateClient);

            Assert.Contains("volume_mwh", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            await RenameVolumeColumnAsync("volume_mwh_drifted", "volume_mwh");
        }
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>(StringComparer.Ordinal)
                        {
                            ["Database:ConnectionString"] = Postgres.ConnectionString,
                            ["Jwt:Issuer"] = "Tradebook",
                            ["Jwt:Audience"] = "Tradebook",
                            ["Jwt:SigningKey"] = CustomWebApplicationFactory.JwtSigningKey,
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
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            var command = new NpgsqlCommand(sql, connection);
            await using (command.ConfigureAwait(false))
            {
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }
    }
}
