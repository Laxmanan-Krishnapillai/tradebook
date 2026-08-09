using Tradebook.Api.Security;

namespace Tradebook.UnitTests;

public sealed class JwtSecurityOptionsValidatorTests
{
    private readonly JwtSecurityOptionsValidator _validator = new();

    [Theory]
    [InlineData("too-short")]
    [InlineData("1234567890123456789012345678901")]
    [InlineData("development-only-signing-key-must-be-replaced")]
    public void WeakOrKnownSigningKeysAreRejected(string signingKey)
    {
        var result = _validator.Validate(
            null,
            new JwtOptions
            {
                Issuer = "Tradebook",
                Audience = "Tradebook",
                SigningKey = signingKey,
            }
        );

        Assert.True(result.Failed);
    }

    [Fact]
    public void StrongExternalSigningKeyIsAccepted()
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

    [Fact]
    public void SigningKeyLengthIsMeasuredInUtf8Bytes()
    {
        var result = _validator.Validate(
            null,
            new JwtOptions
            {
                Issuer = "Tradebook",
                Audience = "Tradebook",
                SigningKey = "åååååååååååååååå",
            }
        );

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void WhitespaceOnlyConfigurationFailsClosed()
    {
        var result = _validator.Validate(
            null,
            new JwtOptions
            {
                Issuer = " ",
                Audience = "\t",
                SigningKey = new string(' ', 32),
            }
        );

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
}
