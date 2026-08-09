using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tradebook.Api.Features.Auth.Login;
using Tradebook.Api.Security;
using Tradebook.Core.Domain.Entities;
using Tradebook.Core.DTOs;

namespace Tradebook.UnitTests;

public sealed class LoginEndpointUnitTests
{
    private const string SigningKey = "unit-test-signing-key-with-enough-entropy-123456";
    private static readonly DateTimeOffset TestUtcNow = new(2026, 8, 9, 10, 0, 0, TimeSpan.Zero);

    private static IOptions<JwtOptions> Options() =>
        Microsoft.Extensions.Options.Options.Create(
            new JwtOptions
            {
                SigningKey = SigningKey,
                Issuer = "TestIssuer",
                Audience = "TestAudience",
            }
        );

    private static (LoginEndpoint Endpoint, FakeUserRepository Users) CreateEndpoint()
    {
        var users = new FakeUserRepository();
        var endpoint = Factory.Create<LoginEndpoint>(
            ctx => ctx.AddTestServices(services => services.AddHttpContextAccessor()),
            users,
            Options(),
            new FixedTimeProvider(TestUtcNow)
        );
        return (endpoint, users);
    }

    private static User ActiveUser(string username, string password, params string[] roles) =>
        new()
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = PasswordHasher.Hash(password),
            Roles = roles,
            IsActive = true,
        };

    [Fact]
    public async Task ValidCredentialsReturnSignedTokenWithSubAndRoleClaims()
    {
        var (endpoint, users) = CreateEndpoint();
        var user = ActiveUser("trader1", "S3cure!passphrase", "Trader", "Admin");
        users.Users[user.Username] = user;

        await endpoint.HandleAsync(new LoginRequest("trader1", "S3cure!passphrase"), default);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(endpoint.Response.AccessToken);
        Assert.Equal("TestIssuer", token.Issuer);
        Assert.Contains("TestAudience", token.Audiences, StringComparer.Ordinal);
        Assert.Equal(
            user.Id.Value.ToString(),
            token
                .Claims.Single(claim => string.Equals(claim.Type, "sub", StringComparison.Ordinal))
                .Value
        );
        var roles = token
            .Claims.Where(claim => claim.Type is "role" or ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToArray();
        Assert.Equal(["Trader", "Admin"], roles);

        var expectedExpiry = TestUtcNow.AddHours(8);
        Assert.Equal(expectedExpiry, endpoint.Response.ExpiresAtUtc);
        Assert.Equal(expectedExpiry.UtcDateTime, token.ValidTo);
    }

    [Fact]
    public async Task UnknownUserWrongPasswordAndInactiveUserAllGet401()
    {
        var (_, users) = CreateEndpoint();
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

        foreach (
            var request in new[]
            {
                new LoginRequest("nobody", "S3cure!passphrase"),
                new LoginRequest("trader1", "wrong"),
                new LoginRequest("leaver", "S3cure!passphrase"),
            }
        )
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

    [Fact]
    public void PasswordHasherRejectsAValidPayloadWithAnUnrecognizedScheme()
    {
        const string password = "S3cure!passphrase";
        var encodedHash = PasswordHasher.Hash(password);
        var wrongSchemeHash = $"argon2id.{encodedHash[(encodedHash.IndexOf('.') + 1)..]}";

        Assert.False(PasswordHasher.Verify(password, wrongSchemeHash));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
