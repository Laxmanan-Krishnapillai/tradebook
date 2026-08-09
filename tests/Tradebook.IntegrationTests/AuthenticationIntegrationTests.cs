using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Tradebook.IntegrationTests.Fixtures;

namespace Tradebook.IntegrationTests;

public sealed class AuthenticationIntegrationTests(PostgresTestFixture postgres)
    : PostgresDatabaseTestBase(postgres)
{
    [Fact]
    public async Task ApiRoutesRequireABearerTokenWhileHealthProbesAreAnonymous()
    {
        await using var factory = CreateFactory(Postgres.ConnectionString);
        using var client = factory.CreateClient();

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/deliveries")).StatusCode
        );
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task ReadinessFailsButLivenessStaysHealthyWhenPostgresIsUnavailable()
    {
        const string unavailableDatabase =
            "Host=127.0.0.1;Port=1;Database=tradebook;Username=tradebook;Password=tradebook;Timeout=1;Command Timeout=1";
        await using var factory = CreateFactory(unavailableDatabase);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            (await client.GetAsync("/health/ready")).StatusCode
        );
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>(StringComparer.Ordinal)
                        {
                            ["Database:ConnectionString"] = connectionString,
                            ["Jwt:Issuer"] = "Tradebook",
                            ["Jwt:Audience"] = "Tradebook",
                            ["Jwt:SigningKey"] = CustomWebApplicationFactory.JwtSigningKey,
                        }
                    )
            )
        );
}
