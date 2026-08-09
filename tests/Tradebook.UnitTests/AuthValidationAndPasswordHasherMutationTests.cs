using System.Security.Cryptography;
using AwesomeAssertions;
using Tradebook.Api.Features.Auth.Login;
using Tradebook.Core.DTOs;

namespace Tradebook.UnitTests;

public sealed class AuthValidationAndPasswordHasherMutationTests
{
    private readonly LoginValidator _validator = new();

    [Theory]
    [InlineData(1, 1)]
    [InlineData(100, 200)]
    public void LoginValidatorAcceptsInclusiveUsernameAndPasswordLengthBoundaries(
        int usernameLength,
        int passwordLength
    )
    {
        var result = _validator.Validate(
            new LoginRequest(new string('u', usernameLength), new string('p', passwordLength))
        );

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("null-username", "Username")]
    [InlineData("empty-username", "Username")]
    [InlineData("blank-username", "Username")]
    [InlineData("null-password", "Password")]
    [InlineData("empty-password", "Password")]
    [InlineData("blank-password", "Password")]
    public void LoginValidatorRejectsEachMissingOrBlankCredential(
        string scenario,
        string expectedProperty
    )
    {
        var request = scenario switch
        {
            "null-username" => new LoginRequest(null!, "password"),
            "empty-username" => new LoginRequest(string.Empty, "password"),
            "blank-username" => new LoginRequest(" \t ", "password"),
            "null-password" => new LoginRequest("username", null!),
            "empty-password" => new LoginRequest("username", string.Empty),
            "blank-password" => new LoginRequest("username", " \t "),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == expectedProperty);
    }

    [Theory]
    [InlineData(101, 1, "Username")]
    [InlineData(1, 201, "Password")]
    public void LoginValidatorRejectsTheFirstLengthAboveEachMaximum(
        int usernameLength,
        int passwordLength,
        string expectedProperty
    )
    {
        var result = _validator.Validate(
            new LoginRequest(new string('u', usernameLength), new string('p', passwordLength))
        );

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == expectedProperty);
    }

    [Fact]
    public void HashEmitsTheExactDocumentedSchemeIterationSaltAndKeyFormat()
    {
        const string password = "correct horse battery staple";

        var encoded = PasswordHasher.Hash(password);
        var parts = encoded.Split('.');

        parts.Should().HaveCount(4);
        parts[0].Should().Be("pbkdf2-sha256");
        parts[1].Should().Be("210000");
        Convert.FromBase64String(parts[2]).Should().HaveCount(16);
        Convert.FromBase64String(parts[3]).Should().HaveCount(32);
        PasswordHasher.Verify(password, encoded).Should().BeTrue();
    }

    [Fact]
    public void VerifyAcceptsTheExactIterationFloorAndRequiredSaltAndKeySizes()
    {
        const string password = "format-boundary-password";
        var encoded = EncodePbkdf2(password, 210_000, saltLength: 16, hashLength: 32);

        PasswordHasher.Verify(password, encoded).Should().BeTrue();
    }

    [Theory]
    [InlineData("209999")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-an-integer")]
    public void VerifyRejectsEachIterationValueBelowTheFloorOrOutsideTheFormat(string iterationText)
    {
        const string password = "format-boundary-password";
        var valid = EncodePbkdf2(password, 210_000, saltLength: 16, hashLength: 32).Split('.');
        var encoded = string.Join('.', valid[0], iterationText, valid[2], valid[3]);

        PasswordHasher.Verify(password, encoded).Should().BeFalse();
    }

    [Theory]
    [InlineData(15)]
    [InlineData(17)]
    public void VerifyRejectsEachNeighboringSaltLengthEvenWhenTheHashMatches(int saltLength)
    {
        const string password = "format-boundary-password";
        var encoded = EncodePbkdf2(password, 210_000, saltLength, hashLength: 32);

        PasswordHasher.Verify(password, encoded).Should().BeFalse();
    }

    [Theory]
    [InlineData(31)]
    [InlineData(33)]
    public void VerifyRejectsEachNeighboringKeyLengthEvenWhenTheHashMatches(int hashLength)
    {
        const string password = "format-boundary-password";
        var encoded = EncodePbkdf2(password, 210_000, saltLength: 16, hashLength);

        PasswordHasher.Verify(password, encoded).Should().BeFalse();
    }

    [Fact]
    public void RepeatedHashesUseIndependentSaltsAndBothVerify()
    {
        const string password = "same-password";

        var first = PasswordHasher.Hash(password);
        var second = PasswordHasher.Hash(password);
        var firstParts = first.Split('.');
        var secondParts = second.Split('.');

        first.Should().NotBe(second);
        firstParts[2].Should().NotBe(secondParts[2]);
        firstParts[3].Should().NotBe(secondParts[3]);
        PasswordHasher.Verify(password, first).Should().BeTrue();
        PasswordHasher.Verify(password, second).Should().BeTrue();
    }

    [Theory]
    [InlineData("missing-segment")]
    [InlineData("extra-segment")]
    [InlineData("wrong-scheme")]
    [InlineData("wrong-scheme-case")]
    [InlineData("invalid-iterations")]
    [InlineData("invalid-salt-base64")]
    [InlineData("invalid-hash-base64")]
    public void VerifyRejectsEachIndependentlyMalformedHashComponent(string scenario)
    {
        const string password = "S3cure!passphrase";
        var parts = PasswordHasher.Hash(password).Split('.');
        var encoded = scenario switch
        {
            "missing-segment" => string.Join('.', parts.Take(3)),
            "extra-segment" => string.Join('.', parts.Append("extra")),
            "wrong-scheme" => string.Join('.', "argon2id", parts[1], parts[2], parts[3]),
            "wrong-scheme-case" => string.Join('.', "PBKDF2-SHA256", parts[1], parts[2], parts[3]),
            "invalid-iterations" => string.Join(
                '.',
                parts[0],
                "two-hundred-thousand",
                parts[2],
                parts[3]
            ),
            "invalid-salt-base64" => string.Join('.', parts[0], parts[1], "!!", parts[3]),
            "invalid-hash-base64" => string.Join('.', parts[0], parts[1], parts[2], "!!"),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };

        PasswordHasher.Verify(password, encoded).Should().BeFalse();
    }

    [Fact]
    public void VerifyRejectsWrongPasswordIterationSaltAndHashIndependently()
    {
        const string password = "S3cure!passphrase";
        var encoded = PasswordHasher.Hash(password);
        var parts = encoded.Split('.');

        PasswordHasher.Verify("different-password", encoded).Should().BeFalse();

        var changedIterations = string.Join('.', parts[0], "210001", parts[2], parts[3]);
        PasswordHasher.Verify(password, changedIterations).Should().BeFalse();

        var salt = Convert.FromBase64String(parts[2]);
        salt[0] ^= 0x01;
        var changedSalt = string.Join(
            '.',
            parts[0],
            parts[1],
            Convert.ToBase64String(salt),
            parts[3]
        );
        PasswordHasher.Verify(password, changedSalt).Should().BeFalse();

        var expected = Convert.FromBase64String(parts[3]);
        expected[^1] ^= 0x01;
        var changedHash = string.Join(
            '.',
            parts[0],
            parts[1],
            parts[2],
            Convert.ToBase64String(expected)
        );
        PasswordHasher.Verify(password, changedHash).Should().BeFalse();
    }

    [Fact]
    public void HashOutputMatchesAnIndependentPbkdf2Sha256Derivation()
    {
        const string password = "independent-derivation-check";
        var encoded = PasswordHasher.Hash(password);
        var parts = encoded.Split('.');
        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);

        var independentlyDerived = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            210_000,
            HashAlgorithmName.SHA256,
            32
        );

        CryptographicOperations.FixedTimeEquals(independentlyDerived, expected).Should().BeTrue();
    }

    private static string EncodePbkdf2(
        string password,
        int iterations,
        int saltLength,
        int hashLength
    )
    {
        var salt = Enumerable.Range(1, saltLength).Select(value => (byte)value).ToArray();
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            hashLength
        );
        return $"pbkdf2-sha256.{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }
}
