using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FastEndpoints;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Auth.Login;

public sealed class LoginEndpoint(
    IUserRepository users,
    IOptions<JwtOptions> options,
    TimeProvider timeProvider
) : Endpoint<LoginRequest, LoginResponse>
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(8);

    public override void Configure()
    {
        Post("/api/v1/auth/login");
        AllowAnonymous(); // sole anonymous API route (Task 02 §3.8, D11)
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        var user = await (users.GetByUsernameAsync(req.Username, ct)).ConfigureAwait(false);

        // 401 with no detail on which factor failed (§3.8). Verify against a dummy hash on
        // unknown usernames to keep the response time independent of user existence.
        var passwordOk = user is not null
            ? PasswordHasher.Verify(req.Password, user.PasswordHash)
            : PasswordHasher.Verify(req.Password, UnknownUserHash);
        if (user is null || !user.IsActive || !passwordOk)
        {
            await (Send.UnauthorizedAsync(ct)).ConfigureAwait(false);
            return;
        }

        var expiresAt = timeProvider.GetUtcNow().Add(TokenLifetime);
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, user.Id.Value.ToString()) };
        claims.AddRange(user.Roles.Select(role => new Claim("role", role)));

        var jwt = options.Value;
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));
        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        );

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        await (
            Send.OkAsync(LoginMapper.ToResponse(user, accessToken, expiresAt), cancellation: ct)
        ).ConfigureAwait(false);
    }

    private static readonly string UnknownUserHash = PasswordHasher.Hash(Guid.NewGuid().ToString());
}
