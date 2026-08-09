using System.Globalization;
using AwesomeAssertions;
using Tradebook.Api.Features.Auth.Login;
using Tradebook.Core.DTOs;

namespace Tradebook.UnitTests;

public sealed class AuthTests
{
    [Fact]
    public void HashThenVerifyRoundTrips()
    {
        var hash = PasswordHasher.Hash("correct horse battery staple");
        PasswordHasher.Verify("correct horse battery staple", hash).Should().BeTrue();
    }

    [Fact]
    public void VerifyRejectsWrongPassword()
    {
        var hash = PasswordHasher.Hash("correct horse battery staple");
        PasswordHasher.Verify("Tr0ub4dor&3", hash).Should().BeFalse();
    }

    [Fact]
    public void HashesAreSaltedAndMeetIterationFloor()
    {
        var first = PasswordHasher.Hash("same-password");
        var second = PasswordHasher.Hash("same-password");
        first.Should().NotBe(second);
        first.Should().StartWith("pbkdf2-sha256.");
        int.Parse(first.Split('.')[1], CultureInfo.InvariantCulture)
            .Should()
            .BeGreaterThanOrEqualTo(210_000);
    }

    [Theory]
    [InlineData("not-a-hash")]
    [InlineData("pbkdf2-sha256.abc.!!.!!")]
    [InlineData("md5.1000.c2FsdA==.aGFzaA==")]
    public void VerifyRejectsMalformedOrForeignHashes(string encoded)
    {
        PasswordHasher.Verify("anything", encoded).Should().BeFalse();
    }

    [Fact]
    public void LoginValidatorRequiresUsernameAndPassword()
    {
        new LoginValidator().Validate(new LoginRequest("", "secret")).IsValid.Should().BeFalse();
        new LoginValidator().Validate(new LoginRequest("trader", "")).IsValid.Should().BeFalse();
        new LoginValidator()
            .Validate(new LoginRequest("trader", "secret"))
            .IsValid.Should()
            .BeTrue();
    }
}
