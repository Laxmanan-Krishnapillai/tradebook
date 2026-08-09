using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tradebook.Api.Features.Auth.Login;
using Tradebook.Api.Security;
using Tradebook.Core.Domain.Entities;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

public sealed class LoginEndpointBranchMutationTests
{
    private const string SigningKey = "branch-test-signing-key-with-enough-entropy-123456";

    [Fact]
    public async Task ActiveUserWithoutRolesReturnsItsActorIdAndASubjectOnlyToken()
    {
        const string username = "roleless-user";
        const string password = "S3cure!passphrase";
        var user = User(username, PasswordHasher.Hash(password), isActive: true, roles: []);
        var users = new RecordingUserRepository(user);
        using var cancellation = new CancellationTokenSource();
        var endpoint = CreateEndpoint(users);

        await endpoint.HandleAsync(new LoginRequest(username, password), cancellation.Token);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal(user.Id, endpoint.Response.ActorId);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(endpoint.Response.AccessToken);
        Assert.Equal(
            user.Id.Value.ToString(),
            token
                .Claims.Single(claim => string.Equals(claim.Type, "sub", StringComparison.Ordinal))
                .Value
        );
        Assert.DoesNotContain(token.Claims, claim => claim.Type is "role" or ClaimTypes.Role);
        Assert.Equal(1, users.Calls);
        Assert.Equal(username, users.Username);
        Assert.Equal(cancellation.Token, users.CancellationToken);
    }

    [Theory]
    [InlineData("missing-user")]
    [InlineData("inactive-user")]
    [InlineData("wrong-password")]
    [InlineData("malformed-hash")]
    public async Task EachInvalidCredentialBranchIndependentlyReturns401(string scenario)
    {
        const string username = "trader";
        const string correctPassword = "S3cure!passphrase";
        var requestedPassword = correctPassword;
        User? user = scenario switch
        {
            "missing-user" => null,
            "inactive-user" => User(
                username,
                PasswordHasher.Hash(correctPassword),
                isActive: false,
                roles: ["Trader"]
            ),
            "wrong-password" => User(
                username,
                PasswordHasher.Hash(correctPassword),
                isActive: true,
                roles: ["Trader"]
            ),
            "malformed-hash" => User(
                username,
                "pbkdf2-sha256.209999.c2FsdA==.aGFzaA==",
                isActive: true,
                roles: ["Trader"]
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        if (string.Equals(scenario, "wrong-password", StringComparison.Ordinal))
            requestedPassword = "definitely-wrong";

        var users = new RecordingUserRepository(user);
        using var cancellation = new CancellationTokenSource();
        var endpoint = CreateEndpoint(users);

        await endpoint.HandleAsync(
            new LoginRequest(username, requestedPassword),
            cancellation.Token
        );

        Assert.Equal(401, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal(1, users.Calls);
        Assert.Equal(username, users.Username);
        Assert.Equal(cancellation.Token, users.CancellationToken);
    }

    private static LoginEndpoint CreateEndpoint(IUserRepository users) =>
        Factory.Create<LoginEndpoint>(
            context => context.AddTestServices(services => services.AddHttpContextAccessor()),
            users,
            Microsoft.Extensions.Options.Options.Create(
                new JwtOptions
                {
                    SigningKey = SigningKey,
                    Issuer = "BranchTestIssuer",
                    Audience = "BranchTestAudience",
                }
            ),
            TimeProvider.System
        );

    private static User User(string username, string passwordHash, bool isActive, string[] roles) =>
        new()
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = passwordHash,
            IsActive = isActive,
            Roles = roles,
        };

    private sealed class RecordingUserRepository(User? result) : IUserRepository
    {
        public int Calls { get; private set; }
        public string? Username { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
        {
            Calls++;
            Username = username;
            CancellationToken = cancellationToken;
            return Task.FromResult(result);
        }
    }
}
