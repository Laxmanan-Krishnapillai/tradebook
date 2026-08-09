using AwesomeAssertions;
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
        decimal? realised = 11m
    ) =>
        new(
            contractId ?? Guid.NewGuid(),
            instanceId,
            bookType,
            supplyMonth ?? new DateOnly(2023, 9, 1),
            null,
            nominated,
            realised,
            "TTF",
            null,
            null
        );

    [Theory]
    [InlineData("Sourcing")]
    [InlineData("Sales")]
    [InlineData("Intercompany")]
    public void EveryWhitelistedBookTypePasses(string bookType) =>
        new CreatePhysicalDeliveryValidator()
            .Validate(ValidCreate(bookType: bookType))
            .IsValid.Should()
            .BeTrue();

    [Fact]
    public void EmptyContractIdFails() =>
        new CreatePhysicalDeliveryValidator()
            .Validate(ValidCreate(contractId: Guid.Empty))
            .IsValid.Should()
            .BeFalse();

    [Fact]
    public void OmittedInstanceIdIsGeneratedByTheDatabase() =>
        new CreatePhysicalDeliveryValidator()
            .Validate(ValidCreate(instanceId: null!))
            .IsValid.Should()
            .BeTrue();

    [Fact]
    public void EmptyInstanceIdFails() =>
        new CreatePhysicalDeliveryValidator()
            .Validate(ValidCreate(instanceId: string.Empty))
            .IsValid.Should()
            .BeFalse();

    [Fact]
    public void InstanceIdLongerThan120CharsFails() =>
        new CreatePhysicalDeliveryValidator()
            .Validate(ValidCreate(instanceId: new string('x', 121)))
            .IsValid.Should()
            .BeFalse();

    [Theory]
    [InlineData("Invalid")]
    [InlineData("")]
    public void UnknownBookTypeFails(string bookType) =>
        new CreatePhysicalDeliveryValidator()
            .Validate(ValidCreate(bookType: bookType))
            .IsValid.Should()
            .BeFalse();

    [Fact]
    public void DefaultSupplyMonthFails() =>
        new CreatePhysicalDeliveryValidator()
            .Validate(ValidCreate(supplyMonth: default(DateOnly)))
            .IsValid.Should()
            .BeFalse();

    [Fact]
    public void NegativeNominatedVolumeFails() =>
        new CreatePhysicalDeliveryValidator()
            .Validate(ValidCreate(nominated: -1m))
            .IsValid.Should()
            .BeFalse();

    [Fact]
    public void NegativeRealisedVolumeFails() =>
        new CreatePhysicalDeliveryValidator()
            .Validate(ValidCreate(realised: -1m))
            .IsValid.Should()
            .BeFalse();
}
