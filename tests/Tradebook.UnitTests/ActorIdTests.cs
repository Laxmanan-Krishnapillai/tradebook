using System.Security.Claims;
using Tradebook.Api.Security;

namespace Tradebook.UnitTests;
public sealed class ActorIdTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    [Fact] public void Uses_oid_only_after_tenant_validation() { var actor=Guid.NewGuid(); Assert.Equal(actor, ActorId.From(Principal(actor.ToString(), Tenant.ToString(), Tenant.ToString()))); }
    [Theory] [InlineData("bad", "same", "same")] [InlineData("00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000002", "00000000-0000-0000-0000-000000000001")]
    public void Rejects_malformed_oid_or_tenant_mismatch(string oid,string tid,string validated) => Assert.Throws<UnauthorizedAccessException>(() => ActorId.From(Principal(oid,tid,validated)));
    [Fact] public void Ignores_email_username_name_and_subject() { var actor=Guid.NewGuid(); var p=Principal(actor.ToString(),Tenant.ToString(),Tenant.ToString()); ((ClaimsIdentity)p.Identity!).AddClaims([new("email",Guid.NewGuid().ToString()),new("preferred_username",Guid.NewGuid().ToString()),new("sub",Guid.NewGuid().ToString())]); Assert.Equal(actor,ActorId.From(p)); }
    private static ClaimsPrincipal Principal(string oid,string tid,string validated) => new(new ClaimsIdentity([new Claim("oid",oid),new("tid",tid),new("tradebook_tenant",validated)],"test"));
}
