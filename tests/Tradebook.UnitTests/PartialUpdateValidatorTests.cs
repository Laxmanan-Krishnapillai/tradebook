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
    private static readonly bool EmptyPhysicalDeliveryIsValid =
        new UpdatePhysicalDeliveryValidator()
            .Validate(new UpdatePhysicalDeliveryRequest(Guid.NewGuid(), null, null, 1))
            .IsValid;
    private static readonly bool EmptyCapacityBookingIsValid = new UpdateCapacityBookingValidator()
        .Validate(
            new UpdateCapacityBookingRequest(
                Guid.NewGuid(),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                1
            )
        )
        .IsValid;
    private static readonly bool EmptyTransferIsValid = new UpdateTransferValidator()
        .Validate(
            new UpdateTransferRequest(
                Guid.NewGuid(),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                1
            )
        )
        .IsValid;
    private static readonly bool EmptyBioticketIsValid = new UpdateBioticketValidator()
        .Validate(
            new UpdateBioticketRequest(
                Guid.NewGuid(),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                1
            )
        )
        .IsValid;
    private static readonly bool EmptyGooCertificateIsValid = new UpdateGooCertificateValidator()
        .Validate(
            new UpdateGooCertificateTransactionRequest(
                Guid.NewGuid(),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                1
            )
        )
        .IsValid;
    private static readonly bool EmptyHedgeIsValid = new UpdateHedgeValidator()
        .Validate(new UpdateHedgeRequest(Guid.NewGuid(), null, null, 1))
        .IsValid;

    private static readonly bool PhysicalDeliveryWithOneFieldIsValid =
        new UpdatePhysicalDeliveryValidator()
            .Validate(new UpdatePhysicalDeliveryRequest(Guid.NewGuid(), 0m, null, 1))
            .IsValid;
    private static readonly bool CapacityBookingWithOneFieldIsValid =
        new UpdateCapacityBookingValidator()
            .Validate(
                new UpdateCapacityBookingRequest(
                    Guid.NewGuid(),
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    0m,
                    null,
                    null,
                    null,
                    1
                )
            )
            .IsValid;
    private static readonly bool TransferWithOneFieldIsValid = new UpdateTransferValidator()
        .Validate(
            new UpdateTransferRequest(
                Guid.NewGuid(),
                null,
                null,
                null,
                0m,
                null,
                null,
                null,
                null,
                null,
                null,
                1
            )
        )
        .IsValid;
    private static readonly bool BioticketWithOneFieldIsValid = new UpdateBioticketValidator()
        .Validate(
            new UpdateBioticketRequest(
                Guid.NewGuid(),
                null,
                0m,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                1
            )
        )
        .IsValid;
    private static readonly bool GooCertificateWithOneFieldIsValid =
        new UpdateGooCertificateValidator()
            .Validate(
                new UpdateGooCertificateTransactionRequest(
                    Guid.NewGuid(),
                    null,
                    null,
                    null,
                    null,
                    "Processing",
                    null,
                    null,
                    null,
                    null,
                    1
                )
            )
            .IsValid;
    private static readonly bool HedgeWithOneFieldIsValid = new UpdateHedgeValidator()
        .Validate(new UpdateHedgeRequest(Guid.NewGuid(), 0m, null, 1))
        .IsValid;

    [Fact]
    public void EmptyPartialUpdatesAreRejected()
    {
        Assert.False(EmptyPhysicalDeliveryIsValid, "physical delivery update");
        Assert.False(EmptyCapacityBookingIsValid, "capacity booking update");
        Assert.False(EmptyTransferIsValid, "transfer update");
        Assert.False(EmptyBioticketIsValid, "bioticket update");
        Assert.False(EmptyGooCertificateIsValid, "GoO certificate update");
        Assert.False(EmptyHedgeIsValid, "hedge update");
    }

    [Fact]
    public void ASingleSuppliedMutableFieldIsAValidPartialUpdate()
    {
        Assert.True(PhysicalDeliveryWithOneFieldIsValid);
        Assert.True(CapacityBookingWithOneFieldIsValid);
        Assert.True(TransferWithOneFieldIsValid);
        Assert.True(BioticketWithOneFieldIsValid);
        Assert.True(GooCertificateWithOneFieldIsValid);
        Assert.True(HedgeWithOneFieldIsValid);
    }
}
