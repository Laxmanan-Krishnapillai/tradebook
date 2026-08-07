using Tradebook.Api.Security;

namespace Tradebook.UnitTests;

public sealed class JwtOptionsValidatorTests
{
    private readonly JwtOptionsValidator _validator = new();

    [Fact]
    public void Missing_configuration_fails_closed()
    {
        var result = _validator.Validate(null, new JwtOptions());

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("Issuer", StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains("Audience", StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains("SigningKey", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("too-short")]
    [InlineData("development-only-signing-key-must-be-replaced")]
    public void Weak_or_known_signing_keys_are_rejected(string signingKey)
    {
        var result = _validator.Validate(null, new JwtOptions
        {
            Issuer = "Tradebook",
            Audience = "Tradebook",
            SigningKey = signingKey
        });

        Assert.True(result.Failed);
    }

    [Fact]
    public void Strong_external_signing_key_is_accepted()
    {
        var result = _validator.Validate(null, new JwtOptions
        {
            Issuer = "Tradebook",
            Audience = "Tradebook",
            SigningKey = "unit-test-only-key-with-at-least-32-bytes-73cc"
        });

        Assert.True(result.Succeeded);
    }
}
