using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Tradebook.Api.Security;

internal sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IOptions<EntraOptions> entra)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TradebookTesting";
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var value = Request.Headers.Authorization.ToString();
        if (!value.StartsWith("Bearer ", StringComparison.Ordinal)) return Task.FromResult(AuthenticateResult.NoResult());
        try
        {
            var token = new JwtSecurityTokenHandler().ReadJwtToken(value[7..]);
            if (token.ValidTo <= DateTime.UtcNow) return Task.FromResult(AuthenticateResult.Fail("Expired test token."));
            var claims = token.Claims.ToList();
            var subject = claims.FirstOrDefault(c => c.Type is "oid" or "sub")?.Value;
            claims.RemoveAll(c => c.Type is "oid" or "tid" or "tradebook_tenant" or "scp");
            claims.AddRange([new("oid", subject ?? string.Empty), new("tid", entra.Value.TenantId), new("tradebook_tenant", entra.Value.TenantId), new("scp", "access_as_user")]);
            var identity = new ClaimsIdentity(claims, SchemeName, "name", "role");
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
        catch (Exception ex) when (ex is ArgumentException or SecurityTokenException) { return Task.FromResult(AuthenticateResult.Fail("Invalid test token.")); }
    }
}
