using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Tradebook.IntegrationTests.Fixtures;

namespace Tradebook.IntegrationTests;

public sealed class InAppAgentIntegrationTests(CustomWebApplicationFactory factory)
    : DatabaseTestBase(factory)
{
    [Fact]
    [Trait("Category", "InAppAgent")]
    public async Task StatusRequiresAuthentication()
    {
        using var response = await Client.GetAsync("/api/v1/agent/status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "InAppAgent")]
    public async Task AuthenticatedStatusReportsDefaultOffReadOnlyContract()
    {
        Authenticate(Client);

        using var response = await Client.GetAsync("/api/v1/agent/status");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"enabled\":false", payload, StringComparison.Ordinal);
        Assert.Contains("\"readOnly\":true", payload, StringComparison.Ordinal);
        Assert.Contains("\"transport\":\"AG-UI\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"runPath\":\"/api/v1/agent/run\"", payload, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "InAppAgent")]
    public void RunRouteIsAbsentWhenFeatureIsDisabled()
    {
        var routes = Factory
            .Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>();

        Assert.DoesNotContain(
            routes,
            endpoint =>
                string.Equals(
                    endpoint.RoutePattern.RawText,
                    "/api/v1/agent/run",
                    StringComparison.Ordinal
                )
        );
    }

    [Fact]
    [Trait("Category", "InAppAgent")]
    public async Task EnabledRunRouteStillRequiresAuthentication()
    {
        using var enabledFactory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>(StringComparer.Ordinal)
                        {
                            ["InAppAgent:Enabled"] = "true",
                            ["InAppAgent:Endpoint"] = "https://models.example.test",
                            ["InAppAgent:DeploymentName"] = "tradebook-agent",
                        }
                    )
            )
        );
        using var client = enabledFactory.CreateClient();

        using var response = await client.PostAsync(
            "/api/v1/agent/run",
            new StringContent("{}", Encoding.UTF8, "application/json")
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static void Authenticate(HttpClient client) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken()
        );

    private static string CreateToken()
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(CustomWebApplicationFactory.JwtSigningKey)
        );
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim("oid", Guid.NewGuid().ToString()),
                new Claim("role", "Trader"),
            ]),
            Issuer = "Tradebook",
            Audience = "Tradebook",
            Expires = TimeProvider.System.GetUtcNow().UtcDateTime.AddMinutes(5),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
