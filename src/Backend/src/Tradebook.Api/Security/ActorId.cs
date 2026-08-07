using System.Security.Claims;

namespace Tradebook.Api.Security;

internal static class ActorId
{
    public static Guid From(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("sub")?.Value, out var actorId)
            ? actorId
            : throw new UnauthorizedAccessException("JWT sub claim is required.");
}
