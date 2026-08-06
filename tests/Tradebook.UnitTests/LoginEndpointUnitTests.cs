using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FastEndpoints;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tradebook.Api.Features.Auth.Login;
using Tradebook.Core.Domain.Entities;
using Tradebook.Core.DTOs;

namespace Tradebook.UnitTests;

public sealed class LoginEndpointUnitTests
{
    private const string SigningKey = "unit-test-signing-key-with-enough-entropy-123456";

    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = SigningKey,
            ["Jwt:Issuer"] = "TestIssuer",
            ["Jwt:Audience"] = "TestAudience",
        })
        .Build();

    private static (LoginEndpoint Endpoint, FakeUserRepository Users) CreateEndpoint()
    {
        var users = new FakeUserRepository();
        var endpoint = Factory.Create<LoginEndpoint>(
            ctx => ctx.AddTestServices(services => services.AddHttpContextAccessor()),
            users, Configuration());
        return (endpoint, users);
    }

    private static User ActiveUser(string username, string password, params string[] roles) => new()
    {
        Id = Guid.NewGuid(),
        Username = username,
        PasswordHash = PasswordHasher.Hash(password),
        Roles = roles,
        IsActive = true,
    };

    [Fact]
    public async Task Valid_credentials_return_signed_token_with_sub_and_role_claims()
    {
        var (endpoint, users) = CreateEndpoint();
        var user = ActiveUser("trader1", "S3cure!passphrase", "Trader", "Admin");
        users.Users[user.Username] = user;

        await endpoint.HandleAsync(new LoginRequest("trader1", "S3cure!passphrase"), default);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(endpoint.Response.AccessToken);
        Assert.Equal("TestIssuer", token.Issuer);
        Assert.Contains("TestAudience", token.Audiences);
        Assert.Equal(user.Id.ToString(), token.Claims.Single(claim => claim.Type == "sub").Value);
        var roles = token.Claims
            .Where(claim => claim.Type is "role" or ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToArray();
        Assert.Equal(["Trader", "Admin"], roles);

        var expectedExpiry = DateTimeOffset.UtcNow.AddHours(8);
        Assert.InRange(endpoint.Response.ExpiresAtUtc, expectedExpiry.AddMinutes(-5), expectedExpiry.AddMinutes(5));
        Assert.InRange(token.ValidTo, expectedExpiry.AddMinutes(-5).UtcDateTime, expectedExpiry.AddMinutes(5).UtcDateTime);
    }

    [Fact]
    public async Task Unknown_user_wrong_password_and_inactive_user_all_get_401()
    {
        var (endpoint, users) = CreateEndpoint();
        var active = ActiveUser("trader1", "S3cure!passphrase", "Trader");
        users.Users[active.Username] = active;
        var inactive = ActiveUser("leaver", "S3cure!passphrase", "Trader");
        users.Users["leaver"] = new User
        {
            Id = inactive.Id,
            Username = inactive.Username,
            PasswordHash = inactive.PasswordHash,
            Roles = inactive.Roles,
            IsActive = false,
        };

        foreach (var request in new[]
                 {
                     new LoginRequest("nobody", "S3cure!passphrase"),
                     new LoginRequest("trader1", "wrong"),
                     new LoginRequest("leaver", "S3cure!passphrase"),
                 })
        {
            var (freshEndpoint, freshUsers) = CreateEndpoint();
            foreach (var (name, user) in users.Users)
            {
                freshUsers.Users[name] = user;
            }

            await freshEndpoint.HandleAsync(request, default);
            Assert.Equal(401, freshEndpoint.HttpContext.Response.StatusCode);
        }
    }
}
