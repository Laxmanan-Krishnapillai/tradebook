using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Tradebook.Api.Features.Auth.Login;
using Tradebook.Core.DTOs;
using Tradebook.IntegrationTests.Fixtures;

namespace Tradebook.IntegrationTests;

public sealed class LoginEndpointTests(PostgresTestFixture postgres) : PostgresDatabaseTestBase(postgres)
{
    [Fact]
    public async Task Valid_credentials_return_token_that_opens_protected_endpoints()
    {
        var username = $"trader_{Guid.NewGuid():N}"[..20];
        await InsertUserAsync(username, "S3cure!passphrase", isActive: true, roles: ["Trader"]);

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest(username, "S3cure!passphrase"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.True(login.ExpiresAtUtc > DateTimeOffset.UtcNow);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var protectedResponse = await client.GetAsync("/api/v1/events?afterSequence=0&limit=1");
        Assert.Equal(HttpStatusCode.OK, protectedResponse.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_unknown_user_and_inactive_user_all_return_401()
    {
        var username = $"trader_{Guid.NewGuid():N}"[..20];
        await InsertUserAsync(username, "S3cure!passphrase", isActive: true, roles: ["Trader"]);
        var inactive = $"gone_{Guid.NewGuid():N}"[..20];
        await InsertUserAsync(inactive, "S3cure!passphrase", isActive: false, roles: ["Trader"]);

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        foreach (var request in new[]
                 {
                     new LoginRequest(username, "wrong-password"),
                     new LoginRequest("no-such-user", "S3cure!passphrase"),
                     new LoginRequest(inactive, "S3cure!passphrase"),
                 })
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] = Postgres.ConnectionString,
                    ["Jwt:Issuer"] = "Tradebook",
                    ["Jwt:Audience"] = "Tradebook",
                    ["Jwt:SigningKey"] = CustomWebApplicationFactory.JwtSigningKey,
                })));

    private async Task InsertUserAsync(string username, string password, bool isActive, string[] roles)
    {
        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await connection.ExecuteAsync(
            """
            INSERT INTO users (username, password_hash, roles, is_active)
            VALUES (@Username, @PasswordHash, @Roles, @IsActive);
            """,
            new { Username = username, PasswordHash = PasswordHasher.Hash(password), Roles = roles, IsActive = isActive });
    }
}
