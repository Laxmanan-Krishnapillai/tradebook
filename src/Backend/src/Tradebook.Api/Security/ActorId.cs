using System.Security.Claims;

namespace Tradebook.Api.Security;

internal static class ActorId
{
    public static Guid From(ClaimsPrincipal user)
    {
        if (!Guid.TryParse(user.FindFirst("tid")?.Value, out var tokenTenant) ||
            !Guid.TryParse(user.FindFirst("tradebook_tenant")?.Value, out var validatedTenant) ||
            tokenTenant != validatedTenant ||
            !Guid.TryParse(user.FindFirst("oid")?.Value, out var actorId))
            throw new UnauthorizedAccessException("Validated Entra tid and UUID oid claims are required.");
        return actorId;
    }
}
