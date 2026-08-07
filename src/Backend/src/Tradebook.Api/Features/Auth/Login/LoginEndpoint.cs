using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FastEndpoints;
using Microsoft.IdentityModel.Tokens;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Auth.Login;

public sealed class LoginEndpoint(IUserRepository users, IConfiguration configuration)
    : Endpoint<LoginRequest, LoginResponse>
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(8);

    public override void Configure()
    {
        Post("/api/v1/auth/login");
        AllowAnonymous(); // sole anonymous API route (Task 02 §3.8, D11)
    }

    public override async Task HandleAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await users.GetByUsernameAsync(request.Username, cancellationToken);

        // 401 with no detail on which factor failed (§3.8). Verify against a dummy hash on
        // unknown usernames to keep the response time independent of user existence.
        var passwordOk = user is not null
            ? PasswordHasher.Verify(request.Password, user.PasswordHash)
            : PasswordHasher.Verify(request.Password, UnknownUserHash);
        if (user is null || !user.IsActive || !passwordOk)
        {
            await SendUnauthorizedAsync(cancellationToken);
            return;
        }

        var expiresAt = DateTimeOffset.UtcNow.Add(TokenLifetime);
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, user.Id.ToString()) };
        claims.AddRange(user.Roles.Select(role => new Claim("role", role)));

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!));
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        await SendOkAsync(new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expiresAt), cancellationToken);
    }

    private static readonly string UnknownUserHash = PasswordHasher.Hash(Guid.NewGuid().ToString());
}
