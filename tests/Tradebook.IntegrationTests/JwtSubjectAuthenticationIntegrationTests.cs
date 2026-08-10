using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Tradebook.IntegrationTests.Fixtures;

namespace Tradebook.IntegrationTests;

public sealed class JwtSubjectAuthenticationIntegrationTests(PostgresTestFixture postgres)
    : PostgresDatabaseTestBase(postgres)
{
    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task RestApiRejectsTokensWithoutAValidUuidSubject(string? subject)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(subject, includeReadRole: true)
        );

        using var response = await client.GetAsync("/api/v1/events?afterSequence=0&limit=1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task DashboardHubRejectsTokensWithoutAValidUuidSubject(string? subject)
    {
        await using var factory = CreateFactory();
        await using var hub = BuildHubConnection(
            factory,
            CreateToken(subject, includeReadRole: true)
        );

        await Assert.ThrowsAsync<HttpRequestException>(() => hub.StartAsync());
    }

    [Fact]
    public async Task DashboardHubRequiresAReadRole()
    {
        await using var factory = CreateFactory();
        await using var hub = BuildHubConnection(
            factory,
            CreateToken(Guid.NewGuid().ToString(), includeReadRole: false)
        );

        await Assert.ThrowsAsync<HttpRequestException>(() => hub.StartAsync());
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder
                .UseEnvironment("Testing")
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

    private static HubConnection BuildHubConnection(
        WebApplicationFactory<Program> factory,
        string token
    ) =>
        new HubConnectionBuilder()
            .WithUrl(
                new Uri(factory.Server.BaseAddress, "hubs/dashboard"),
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                }
            )
            .Build();

    private static string CreateToken(string? subject, bool includeReadRole)
    {
        var claims = new List<Claim>();
        if (subject is not null)
        {
            claims.Add(new Claim("sub", subject));
        }

        if (includeReadRole)
        {
            claims.Add(new Claim("role", "Admin"));
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(CustomWebApplicationFactory.JwtSigningKey)
        );
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "Tradebook",
            Audience = "Tradebook",
            Subject = new ClaimsIdentity(claims),
            Expires = TimeProvider.System.GetUtcNow().UtcDateTime.AddMinutes(5),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
