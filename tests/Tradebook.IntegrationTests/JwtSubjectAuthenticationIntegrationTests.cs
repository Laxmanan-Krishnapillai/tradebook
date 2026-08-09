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
    public async Task Rest_api_rejects_tokens_without_a_valid_uuid_subject(string? subject)
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
    public async Task Dashboard_hub_rejects_tokens_without_a_valid_uuid_subject(string? subject)
    {
        await using var factory = CreateFactory();
        await using var hub = BuildHubConnection(
            factory,
            CreateToken(subject, includeReadRole: true)
        );

        await Assert.ThrowsAsync<HttpRequestException>(() => hub.StartAsync());
    }

    [Fact]
    public async Task Dashboard_hub_requires_a_read_role()
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
        {
            builder.UseSetting("Database:ConnectionString", Postgres.ConnectionString);
            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["Database:ConnectionString"] = Postgres.ConnectionString,
                            ["Jwt:Issuer"] = "Tradebook",
                            ["Jwt:Audience"] = "Tradebook",
                            ["Jwt:SigningKey"] = CustomWebApplicationFactory.JwtSigningKey,
                        }
                    )
            );
        });

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
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
