using FluentAssertions;
using Tradebook.Api.Features.Auth.Login;
using Tradebook.Core.DTOs;

namespace Tradebook.UnitTests;

public sealed class AuthTests
{
    [Fact]
    public void Hash_then_verify_round_trips()
    {
        var hash = PasswordHasher.Hash("correct horse battery staple");
        PasswordHasher.Verify("correct horse battery staple", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_rejects_wrong_password()
    {
        var hash = PasswordHasher.Hash("correct horse battery staple");
        PasswordHasher.Verify("Tr0ub4dor&3", hash).Should().BeFalse();
    }

    [Fact]
    public void Hashes_are_salted_and_meet_iteration_floor()
    {
        var first = PasswordHasher.Hash("same-password");
        var second = PasswordHasher.Hash("same-password");
        first.Should().NotBe(second);
        first.Should().StartWith("pbkdf2-sha256.");
        int.Parse(first.Split('.')[1]).Should().BeGreaterThanOrEqualTo(210_000);
    }

    [Theory]
    [InlineData("not-a-hash")]
    [InlineData("pbkdf2-sha256.abc.!!.!!")]
    [InlineData("md5.1000.c2FsdA==.aGFzaA==")]
    public void Verify_rejects_malformed_or_foreign_hashes(string encoded)
    {
        PasswordHasher.Verify("anything", encoded).Should().BeFalse();
    }

    [Fact]
    public void Login_validator_requires_username_and_password()
    {
        new LoginValidator().Validate(new LoginRequest("", "secret")).IsValid.Should().BeFalse();
        new LoginValidator().Validate(new LoginRequest("trader", "")).IsValid.Should().BeFalse();
        new LoginValidator().Validate(new LoginRequest("trader", "secret")).IsValid.Should().BeTrue();
    }
}
