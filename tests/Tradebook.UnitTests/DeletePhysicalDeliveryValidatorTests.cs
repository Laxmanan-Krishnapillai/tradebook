using FluentAssertions;
using Tradebook.Api.Features.PhysicalDeliveries.DeletePhysicalDelivery;
using Tradebook.Core.DTOs;

namespace Tradebook.UnitTests;

public sealed class DeletePhysicalDeliveryValidatorTests
{
    private static DeletePhysicalDeliveryRequest ValidRequest(
        Guid? deliveryId = null,
        string reason = "Duplicate entry",
        long version = 1) =>
        new(deliveryId ?? Guid.NewGuid(), reason, version);

    [Fact]
    public void Valid_request_passes() =>
        new DeletePhysicalDeliveryValidator().Validate(ValidRequest()).IsValid.Should().BeTrue();

    [Fact]
    public void Empty_delivery_id_fails() =>
        new DeletePhysicalDeliveryValidator().Validate(ValidRequest(deliveryId: Guid.Empty)).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_reason_fails(string? reason) =>
        new DeletePhysicalDeliveryValidator().Validate(ValidRequest(reason: reason!)).IsValid.Should().BeFalse();

    [Fact]
    public void Reason_longer_than_500_characters_fails() =>
        new DeletePhysicalDeliveryValidator().Validate(ValidRequest(reason: new string('x', 501))).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_version_fails(long version) =>
        new DeletePhysicalDeliveryValidator().Validate(ValidRequest(version: version)).IsValid.Should().BeFalse();
}
