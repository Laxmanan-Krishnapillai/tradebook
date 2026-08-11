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

        using var readinessTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        HttpStatusCode readinessStatus;
        do
        {
            using var readiness = await client.GetAsync("/health/ready", readinessTimeout.Token);
            readinessStatus = readiness.StatusCode;
            if (readinessStatus == HttpStatusCode.OK)
            {
                break;
            }

            Assert.Equal(HttpStatusCode.ServiceUnavailable, readinessStatus);
            await Task.Delay(TimeSpan.FromMilliseconds(100), readinessTimeout.Token);
        } while (true);

        Assert.Equal(HttpStatusCode.OK, readinessStatus);
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
            builder
                .UseEnvironment("Testing")
                .UseSetting("Database:ConnectionString", connectionString)
                .ConfigureAppConfiguration(
                    (_, configuration) =>
                        configuration.AddInMemoryCollection(
                            new Dictionary<string, string?>(StringComparer.Ordinal)
                            {
                                ["Database:ConnectionString"] = connectionString,
                                ["Entra:TenantId"] = "11111111-1111-1111-1111-111111111111",
                                ["Entra:ClientId"] = "22222222-2222-2222-2222-222222222222",
                            }
                        )
                )
        );
}
