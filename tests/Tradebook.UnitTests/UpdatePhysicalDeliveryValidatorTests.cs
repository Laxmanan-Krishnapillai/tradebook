using AwesomeAssertions;
using Tradebook.Api.Features.PhysicalDeliveries.CreatePhysicalDelivery;
using Tradebook.Api.Features.PhysicalDeliveries.UpdatePhysicalDelivery;
using Tradebook.Core.DTOs;

namespace Tradebook.UnitTests;

public sealed class UpdatePhysicalDeliveryValidatorTests
{
    private static UpdatePhysicalDeliveryRequest ValidUpdate(
        Guid? deliveryId = null,
        decimal? realised = 10m,
        string? status = null,
        long version = 1
    ) => new(deliveryId ?? Guid.NewGuid(), realised, status, version);

    [Theory]
    [InlineData(null)]
    [InlineData("Completed - Payment Received/Sent")]
    [InlineData("In Progress - Invoice Received/Sent")]
    [InlineData("Pending - No Invoice")]
    [InlineData("Cancelled")]
    [InlineData("Awaiting")]
    [InlineData("Issue")]
    public void EveryWhitelistedStatusPasses(string? status) =>
        new UpdatePhysicalDeliveryValidator()
            .Validate(ValidUpdate(status: status))
            .IsValid.Should()
            .BeTrue();

    [Theory]
    [InlineData("Deleted")]
    [InlineData("")]
    [InlineData("completed - payment received/sent")]
    public void UnknownStatusFails(string status) =>
        new UpdatePhysicalDeliveryValidator()
            .Validate(ValidUpdate(status: status))
            .IsValid.Should()
            .BeFalse();

    [Fact]
    public void EmptyDeliveryIdFails() =>
        new UpdatePhysicalDeliveryValidator()
            .Validate(ValidUpdate(deliveryId: Guid.Empty))
            .IsValid.Should()
            .BeFalse();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveVersionFails(long version) =>
        new UpdatePhysicalDeliveryValidator()
            .Validate(ValidUpdate(version: version))
            .IsValid.Should()
            .BeFalse();

    [Fact]
    public void NegativeRealisedVolumeFails() =>
        new UpdatePhysicalDeliveryValidator()
            .Validate(ValidUpdate(realised: -1m))
            .IsValid.Should()
            .BeFalse();
}
