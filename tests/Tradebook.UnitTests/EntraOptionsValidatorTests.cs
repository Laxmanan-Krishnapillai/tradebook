using Tradebook.Api.Security;
namespace Tradebook.UnitTests;
public sealed class EntraOptionsValidatorTests
{
 [Fact] public void Accepts_complete_single_tenant_configuration() => Assert.True(new EntraOptionsValidator().Validate(null,new(){TenantId=Guid.NewGuid().ToString(),ClientId=Guid.NewGuid().ToString()}).Succeeded);
 [Fact] public void Rejects_placeholders_and_non_Microsoft_authority() { var result=new EntraOptionsValidator().Validate(null,new(){Instance="https://example.test/",TenantId=Guid.Empty.ToString(),ClientId="bad"}); Assert.False(result.Succeeded); Assert.Equal(3,result.Failures!.Count()); }
}
