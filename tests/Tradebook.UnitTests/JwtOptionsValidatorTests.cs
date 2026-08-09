using Tradebook.Api.Security;

namespace Tradebook.UnitTests;

public sealed class JwtOptionsValidatorTests
{
    private readonly JwtOptionsValidator _validator = new();

    [Fact]
    public void MissingConfigurationFailsClosed()
    {
        var result = _validator.Validate(null, new JwtOptions());

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains("Issuer", StringComparison.Ordinal)
        );
        Assert.Contains(
            result.Failures,
            failure => failure.Contains("Audience", StringComparison.Ordinal)
        );
        Assert.Contains(
            result.Failures,
            failure => failure.Contains("SigningKey", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void CompleteConfigurationIsAccepted()
    {
        var result = _validator.Validate(
            null,
            new JwtOptions
            {
                Issuer = "Tradebook",
                Audience = "Tradebook",
                SigningKey = "unit-test-only-key-with-at-least-32-bytes-73cc",
            }
        );

        Assert.True(result.Succeeded);
    }
}
