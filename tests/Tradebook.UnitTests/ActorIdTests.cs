using System.Security.Claims;
using Tradebook.Api.Security;

namespace Tradebook.UnitTests;

public sealed class ActorIdTests
{
    [Fact]
    public void ParsesSubClaim()
    {
        var actorId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim("sub", actorId.ToString())], "test")
        );
        Assert.Equal(actorId, ActorId.From(principal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public void MissingOrMalformedSubClaimThrows(string? subValue)
    {
        Claim[] claims = subValue is null ? [] : [new Claim("sub", subValue)];
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var exception = Assert.Throws<UnauthorizedAccessException>(() => ActorId.From(principal));
        Assert.Contains("sub claim", exception.Message, StringComparison.Ordinal);
    }
}
