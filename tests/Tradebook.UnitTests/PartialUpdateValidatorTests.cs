using Tradebook.Api.Features.Biotickets;
using Tradebook.Api.Features.CapacityBookings;
using Tradebook.Api.Features.GooCertificates;
using Tradebook.Api.Features.Hedges;
using Tradebook.Api.Features.PhysicalDeliveries.UpdatePhysicalDelivery;
using Tradebook.Api.Features.Transfers;
using Tradebook.Core.DTOs;

namespace Tradebook.UnitTests;

public sealed class PartialUpdateValidatorTests
{
    [Fact]
    public void Empty_partial_updates_are_rejected()
    {
        Assert.False(new UpdatePhysicalDeliveryValidator().Validate(
            new UpdatePhysicalDeliveryRequest(Guid.NewGuid(), null, null, 1)).IsValid,
            "physical delivery update");
        Assert.False(new UpdateCapacityBookingValidator().Validate(
            new UpdateCapacityBookingRequest(
                Guid.NewGuid(), null, null, null, null, null, null, null, null, null, null, 1)).IsValid,
            "capacity booking update");
        Assert.False(new UpdateTransferValidator().Validate(
            new UpdateTransferRequest(
                Guid.NewGuid(), null, null, null, null, null, null, null, null, null, null, 1)).IsValid,
            "transfer update");
        Assert.False(new UpdateBioticketValidator().Validate(
            new UpdateBioticketRequest(
                Guid.NewGuid(), null, null, null, null, null, null, null, null, null, 1)).IsValid,
            "bioticket update");
        Assert.False(new UpdateGooCertificateValidator().Validate(
            new UpdateGooCertificateTransactionRequest(
                Guid.NewGuid(), null, null, null, null, null, null, null, null, null, 1)).IsValid,
            "GoO certificate update");
        Assert.False(new UpdateHedgeValidator().Validate(
            new UpdateHedgeRequest(Guid.NewGuid(), null, null, 1)).IsValid,
            "hedge update");
    }

    [Fact]
    public void A_single_supplied_mutable_field_is_a_valid_partial_update()
    {
        Assert.True(new UpdatePhysicalDeliveryValidator().Validate(
            new UpdatePhysicalDeliveryRequest(Guid.NewGuid(), 0m, null, 1)).IsValid);
        Assert.True(new UpdateCapacityBookingValidator().Validate(
            new UpdateCapacityBookingRequest(
                Guid.NewGuid(), null, null, null, null, null, null, 0m, null, null, null, 1)).IsValid);
        Assert.True(new UpdateTransferValidator().Validate(
            new UpdateTransferRequest(
                Guid.NewGuid(), null, null, null, 0m, null, null, null, null, null, null, 1)).IsValid);
        Assert.True(new UpdateBioticketValidator().Validate(
            new UpdateBioticketRequest(
                Guid.NewGuid(), null, 0m, null, null, null, null, null, null, null, 1)).IsValid);
        Assert.True(new UpdateGooCertificateValidator().Validate(
            new UpdateGooCertificateTransactionRequest(
                Guid.NewGuid(), null, null, null, null, "Processing", null, null, null, null, 1)).IsValid);
        Assert.True(new UpdateHedgeValidator().Validate(
            new UpdateHedgeRequest(Guid.NewGuid(), 0m, null, 1)).IsValid);
    }
}
