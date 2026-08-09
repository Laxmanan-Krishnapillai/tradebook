using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Tradebook.Api.Security;

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<EntraOptions> entra
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TradebookTesting";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var value = Request.Headers.Authorization.ToString();
        var raw = value.StartsWith("Bearer ", StringComparison.Ordinal) ? value[7..] : null;
        // SignalR WebSocket transports cannot set headers; mirror the JwtBearer hub convention.
        raw ??= Request.Query["access_token"].ToString() is { Length: > 0 } queryToken
            ? queryToken
            : null;
        if (raw is null)
            return Task.FromResult(AuthenticateResult.NoResult());
        try
        {
            var token = new JwtSecurityTokenHandler().ReadJwtToken(raw);
            if (token.ValidTo <= TimeProvider.System.GetUtcNow().UtcDateTime)
                return Task.FromResult(AuthenticateResult.Fail("Expired test token."));
            var claims = token.Claims.ToList();
            var subject = claims.FirstOrDefault(c => c.Type is "oid" or "sub")?.Value;
            if (subject is null || !Guid.TryParse(subject, out _))
                return Task.FromResult(
                    AuthenticateResult.Fail("Test token subject must be a UUID.")
                );
            claims.RemoveAll(c => c.Type is "oid" or "tid" or "tradebook_tenant" or "scp");
            claims.AddRange([
                new("oid", subject),
                new("tid", entra.Value.TenantId),
                new("tradebook_tenant", entra.Value.TenantId),
                new("scp", "access_as_user"),
            ]);
            var identity = new ClaimsIdentity(claims, SchemeName, "name", "role");
            return Task.FromResult(
                AuthenticateResult.Success(
                    new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)
                )
            );
        }
        catch (Exception ex) when (ex is ArgumentException or SecurityTokenException)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid test token."));
        }
    }
}
