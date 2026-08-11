using Tradebook.Api.AgentTools;

namespace Tradebook.UnitTests;

public sealed class InAppAgentOptionsValidatorTests
{
    private readonly InAppAgentOptionsValidator _validator = new();

    [Fact]
    public void DisabledAgentNeedsNoProviderConfiguration()
    {
        var result = _validator.Validate(null, new InAppAgentOptions { Enabled = false });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void EnabledAgentRequiresHttpsEndpointAndDeployment()
    {
        var result = _validator.Validate(
            null,
            new InAppAgentOptions { Enabled = true, Endpoint = "http://localhost" }
        );

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains("HTTPS", StringComparison.Ordinal)
        );
        Assert.Contains(
            result.Failures,
            failure => failure.Contains("DeploymentName", StringComparison.Ordinal)
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-uuid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void ConfiguredManagedIdentityMustBeANonPlaceholderUuid(string clientId)
    {
        var result = _validator.Validate(
            null,
            new InAppAgentOptions
            {
                Enabled = true,
                Endpoint = "https://models.example.test",
                DeploymentName = "tradebook-agent",
                ManagedIdentityClientId = clientId,
            }
        );

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains("ManagedIdentityClientId", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void CompleteEnabledConfigurationIsAccepted()
    {
        var result = _validator.Validate(
            null,
            new InAppAgentOptions
            {
                Enabled = true,
                Endpoint = "https://models.example.test",
                DeploymentName = "tradebook-agent",
                ManagedIdentityClientId = Guid.NewGuid().ToString(),
            }
        );

        Assert.True(result.Succeeded);
    }
}
