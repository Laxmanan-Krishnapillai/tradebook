using AwesomeAssertions;
using Tradebook.Api.Features.PhysicalDeliveries.DeletePhysicalDelivery;
using Tradebook.Core.DTOs;

namespace Tradebook.UnitTests;

public sealed class DeletePhysicalDeliveryValidatorTests
{
    private static DeletePhysicalDeliveryRequest ValidRequest(
        Guid? deliveryId = null,
        string reason = "Duplicate entry",
        long version = 1
    ) => new(deliveryId ?? Guid.NewGuid(), reason, version);

    [Fact]
    public void ValidRequestPasses() =>
        new DeletePhysicalDeliveryValidator().Validate(ValidRequest()).IsValid.Should().BeTrue();

    [Fact]
    public void EmptyDeliveryIdFails() =>
        new DeletePhysicalDeliveryValidator()
            .Validate(ValidRequest(deliveryId: Guid.Empty))
            .IsValid.Should()
            .BeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingReasonFails(string? reason) =>
        new DeletePhysicalDeliveryValidator()
            .Validate(ValidRequest(reason: reason!))
            .IsValid.Should()
            .BeFalse();

    [Fact]
    public void ReasonLongerThan500CharactersFails() =>
        new DeletePhysicalDeliveryValidator()
            .Validate(ValidRequest(reason: new string('x', 501)))
            .IsValid.Should()
            .BeFalse();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveVersionFails(long version) =>
        new DeletePhysicalDeliveryValidator()
            .Validate(ValidRequest(version: version))
            .IsValid.Should()
            .BeFalse();
}
