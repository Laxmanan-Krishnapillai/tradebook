using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Tradebook.IntegrationTests.Fixtures;

namespace Tradebook.IntegrationTests;

public sealed class AuthenticationIntegrationTests(PostgresTestFixture postgres) : PostgresDatabaseTestBase(postgres)
{
    [Fact]
    public async Task Api_routes_require_a_bearer_token_while_health_probes_are_anonymous()
    {
        await using var factory = CreateFactory(Postgres.ConnectionString);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/deliveries")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task Readiness_fails_but_liveness_stays_healthy_when_postgres_is_unavailable()
    {
        var connectionString =
            Postgres.ConnectionString + ";Timeout=1;Command Timeout=1";
        await using var factory = CreateFactory(connectionString);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);

        await Postgres.PauseAsync();
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
            Assert.Equal(
                HttpStatusCode.ServiceUnavailable,
                (await client.GetAsync("/health/ready")).StatusCode);
        }
        finally
        {
            await Postgres.UnpauseAsync();
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Database:ConnectionString", connectionString);
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] = connectionString,
                    ["Jwt:Issuer"] = "Tradebook",
                    ["Jwt:Audience"] = "Tradebook",
                    ["Jwt:SigningKey"] = CustomWebApplicationFactory.JwtSigningKey
                }));
        });
}
