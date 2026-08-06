using FluentAssertions;
using Tradebook.Api.Features.PhysicalDeliveries.CreatePhysicalDelivery;
using Tradebook.Api.Features.PhysicalDeliveries.UpdatePhysicalDelivery;
using Tradebook.Core.DTOs;

namespace Tradebook.UnitTests;

public sealed class CreatePhysicalDeliveryValidatorTests
{
    private static CreatePhysicalDeliveryRequest ValidCreate(
        Guid? contractId = null,
        string instanceId = "BGEM45.SG.2001.NOQS-9-2023",
        string bookType = "Sales",
        DateOnly? supplyMonth = null,
        decimal? nominated = 12m,
        decimal? realised = 11m) =>
        new(contractId ?? Guid.NewGuid(), instanceId, bookType, supplyMonth ?? new DateOnly(2023, 9, 1), null, nominated, realised, "TTF", null, null);

    [Theory]
    [InlineData("Sourcing")]
    [InlineData("Sales")]
    [InlineData("Intercompany")]
    public void Every_whitelisted_book_type_passes(string bookType) =>
        new CreatePhysicalDeliveryValidator().Validate(ValidCreate(bookType: bookType)).IsValid.Should().BeTrue();

    [Fact]
    public void Empty_contract_id_fails() =>
        new CreatePhysicalDeliveryValidator().Validate(ValidCreate(contractId: Guid.Empty)).IsValid.Should().BeFalse();

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Missing_instance_id_fails(string? instanceId) =>
        new CreatePhysicalDeliveryValidator().Validate(ValidCreate(instanceId: instanceId!)).IsValid.Should().BeFalse();

    [Fact]
    public void Instance_id_longer_than_120_chars_fails() =>
        new CreatePhysicalDeliveryValidator().Validate(ValidCreate(instanceId: new string('x', 121))).IsValid.Should().BeFalse();

    [Theory]
    [InlineData("Invalid")]
    [InlineData("")]
    public void Unknown_book_type_fails(string bookType) =>
        new CreatePhysicalDeliveryValidator().Validate(ValidCreate(bookType: bookType)).IsValid.Should().BeFalse();

    [Fact]
    public void Default_supply_month_fails() =>
        new CreatePhysicalDeliveryValidator().Validate(ValidCreate(supplyMonth: default(DateOnly))).IsValid.Should().BeFalse();

    [Fact]
    public void Negative_nominated_volume_fails() =>
        new CreatePhysicalDeliveryValidator().Validate(ValidCreate(nominated: -1m)).IsValid.Should().BeFalse();

    [Fact]
    public void Negative_realised_volume_fails() =>
        new CreatePhysicalDeliveryValidator().Validate(ValidCreate(realised: -1m)).IsValid.Should().BeFalse();
}

public sealed class UpdatePhysicalDeliveryValidatorTests
{
    private static UpdatePhysicalDeliveryRequest ValidUpdate(
        Guid? deliveryId = null,
        decimal? realised = 10m,
        string? status = null,
        long version = 1) =>
        new(deliveryId ?? Guid.NewGuid(), realised, status, version);

    [Theory]
    [InlineData(null)]
    [InlineData("Completed - Payment Received/Sent")]
    [InlineData("In Progress - Invoice Received/Sent")]
    [InlineData("Pending - No Invoice")]
    [InlineData("Cancelled")]
    [InlineData("Awaiting")]
    [InlineData("Issue")]
    public void Every_whitelisted_status_passes(string? status) =>
        new UpdatePhysicalDeliveryValidator().Validate(ValidUpdate(status: status)).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("Deleted")]
    [InlineData("")]
    [InlineData("completed - payment received/sent")]
    public void Unknown_status_fails(string status) =>
        new UpdatePhysicalDeliveryValidator().Validate(ValidUpdate(status: status)).IsValid.Should().BeFalse();

    [Fact]
    public void Empty_delivery_id_fails() =>
        new UpdatePhysicalDeliveryValidator().Validate(ValidUpdate(deliveryId: Guid.Empty)).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_version_fails(long version) =>
        new UpdatePhysicalDeliveryValidator().Validate(ValidUpdate(version: version)).IsValid.Should().BeFalse();

    [Fact]
    public void Negative_realised_volume_fails() =>
        new UpdatePhysicalDeliveryValidator().Validate(ValidUpdate(realised: -1m)).IsValid.Should().BeFalse();
}
