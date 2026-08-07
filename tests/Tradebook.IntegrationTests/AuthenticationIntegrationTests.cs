using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Tradebook.IntegrationTests;

public sealed class AuthenticationIntegrationTests(PostgresTestFixture postgres) : IClassFixture<PostgresTestFixture>
{
    [Fact]
    public async Task Api_routes_require_a_bearer_token_while_health_probes_are_anonymous()
    {
        await using var factory = CreateFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/deliveries")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task Readiness_fails_but_liveness_stays_healthy_when_postgres_is_unavailable()
    {
        const string unavailableDatabase =
            "Host=127.0.0.1;Port=1;Database=tradebook;Username=tradebook;Password=tradebook;Timeout=1;Command Timeout=1";
        await using var factory = CreateFactory(unavailableDatabase);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] = connectionString
                })));
}
