using FluentValidation.Results;
using Tradebook.Api.Features.CapacityBookings;
using Tradebook.Core.DTOs;

namespace Tradebook.UnitTests;

public sealed class CapacityBookingValidatorTests
{
    private static CreateCapacityBookingRequest ValidCreate() =>
        new(
            ContractId: Guid.NewGuid(),
            SupplyMonth: new DateOnly(2026, 1, 1),
            ContractInstanceId: "NRGD.49.GAS.THE.CBC.MON-1-2026",
            CounterpartyId: Guid.NewGuid(),
            BalancingGroup: "NRGD",
            PriceMechanism: "GTF/THE - Yearly",
            StartArea: "GTF",
            EndArea: "THE",
            ShipFix: "GTF-ELLUND-THE",
            BorderPoint: "ELLUND",
            StartDay: new DateOnly(2026, 1, 1),
            EndDay: new DateOnly(2026, 1, 31),
            CapacityMw: 10m,
            CapacityPriceEurMwh: 2m,
            CapacityCostEur: 20m,
            Comments: null
        );

    private static UpdateCapacityBookingRequest EmptyUpdate() =>
        new(
            CapacityBookingId: Guid.NewGuid(),
            BalancingGroup: null,
            PriceMechanism: null,
            StartArea: null,
            EndArea: null,
            StartDay: null,
            EndDay: null,
            CapacityMw: null,
            CapacityPriceEurMwh: null,
            CapacityCostEur: null,
            Comments: null,
            Version: 1
        );

    private static void AssertValid(ValidationResult result) =>
        Assert.True(
            result.IsValid,
            string.Join(Environment.NewLine, result.Errors.Select(error => error.ErrorMessage))
        );

    private static void AssertError(ValidationResult result, string propertyName)
    {
        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => string.Equals(error.PropertyName, propertyName, StringComparison.Ordinal)
        );
    }

    [Fact]
    public void ValidCreateRequestPasses() =>
        AssertValid(new CreateCapacityBookingValidator().Validate(ValidCreate()));

    [Fact]
    public void CreateRequiresContractIdentityAndAFirstOfMonthSupplyMonth()
    {
        var validator = new CreateCapacityBookingValidator();

        AssertError(
            validator.Validate(ValidCreate() with { ContractId = Guid.Empty }),
            nameof(CreateCapacityBookingRequest.ContractId)
        );
        AssertError(
            validator.Validate(ValidCreate() with { SupplyMonth = default }),
            nameof(CreateCapacityBookingRequest.SupplyMonth)
        );
        AssertError(
            validator.Validate(ValidCreate() with { SupplyMonth = new DateOnly(2026, 1, 2) }),
            nameof(CreateCapacityBookingRequest.SupplyMonth)
        );
    }

    [Fact]
    public void CreateContractInstanceLengthHonorsNullAndExact120CharacterBoundary()
    {
        var validator = new CreateCapacityBookingValidator();

        AssertValid(validator.Validate(ValidCreate() with { ContractInstanceId = null }));
        AssertValid(
            validator.Validate(ValidCreate() with { ContractInstanceId = new string('x', 120) })
        );
        AssertError(
            validator.Validate(ValidCreate() with { ContractInstanceId = new string('x', 121) }),
            nameof(CreateCapacityBookingRequest.ContractInstanceId)
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("GTF/THE - Yearly")]
    [InlineData("GTF/THE - Monthly")]
    [InlineData("THE/GTF - Yearly")]
    [InlineData("THE/GTF - Monthly")]
    public void CreateAndUpdateAcceptEveryAuthoritativePriceMechanism(string? priceMechanism)
    {
        AssertValid(
            new CreateCapacityBookingValidator().Validate(
                ValidCreate() with
                {
                    PriceMechanism = priceMechanism,
                }
            )
        );
        AssertValid(
            new UpdateCapacityBookingValidator().Validate(
                EmptyUpdate() with
                {
                    BalancingGroup = "NRGD",
                    PriceMechanism = priceMechanism,
                }
            )
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("GTF/THE - Daily")]
    [InlineData("gtf/the - yearly")]
    public void CreateAndUpdateRejectUnknownOrNoncanonicalPriceMechanisms(string priceMechanism)
    {
        AssertError(
            new CreateCapacityBookingValidator().Validate(
                ValidCreate() with
                {
                    PriceMechanism = priceMechanism,
                }
            ),
            nameof(CreateCapacityBookingRequest.PriceMechanism)
        );
        AssertError(
            new UpdateCapacityBookingValidator().Validate(
                EmptyUpdate() with
                {
                    PriceMechanism = priceMechanism,
                }
            ),
            nameof(UpdateCapacityBookingRequest.PriceMechanism)
        );
    }

    [Fact]
    public void CreateDateRangeAcceptsPartialAndEqualBoundsButRejectsReverseOrder()
    {
        var validator = new CreateCapacityBookingValidator();
        var day = new DateOnly(2026, 1, 15);

        AssertValid(validator.Validate(ValidCreate() with { StartDay = day, EndDay = day }));
        AssertValid(validator.Validate(ValidCreate() with { StartDay = day, EndDay = null }));
        AssertValid(validator.Validate(ValidCreate() with { StartDay = null, EndDay = day }));
        AssertError(
            validator.Validate(ValidCreate() with { StartDay = day, EndDay = day.AddDays(-1) }),
            nameof(CreateCapacityBookingRequest.EndDay)
        );
    }

    [Fact]
    public void UpdateDateRangeAcceptsPartialAndEqualBoundsButRejectsReverseOrder()
    {
        var validator = new UpdateCapacityBookingValidator();
        var day = new DateOnly(2026, 1, 15);

        AssertValid(validator.Validate(EmptyUpdate() with { StartDay = day, EndDay = day }));
        AssertValid(validator.Validate(EmptyUpdate() with { StartDay = day }));
        AssertValid(validator.Validate(EmptyUpdate() with { EndDay = day }));
        AssertError(
            validator.Validate(EmptyUpdate() with { StartDay = day, EndDay = day.AddDays(-1) }),
            nameof(UpdateCapacityBookingRequest.EndDay)
        );
    }

    [Fact]
    public void CreateAndUpdateCapacityAcceptZeroAndRejectNegativeValues()
    {
        AssertValid(
            new CreateCapacityBookingValidator().Validate(ValidCreate() with { CapacityMw = 0m })
        );
        AssertError(
            new CreateCapacityBookingValidator().Validate(
                ValidCreate() with
                {
                    CapacityMw = -0.01m,
                }
            ),
            nameof(CreateCapacityBookingRequest.CapacityMw)
        );

        AssertValid(
            new UpdateCapacityBookingValidator().Validate(EmptyUpdate() with { CapacityMw = 0m })
        );
        AssertError(
            new UpdateCapacityBookingValidator().Validate(
                EmptyUpdate() with
                {
                    CapacityMw = -0.01m,
                }
            ),
            nameof(UpdateCapacityBookingRequest.CapacityMw)
        );
    }

    [Fact]
    public void UpdateRequiresIdentityAndAPositiveVersion()
    {
        var validator = new UpdateCapacityBookingValidator();

        AssertError(
            validator.Validate(
                EmptyUpdate() with
                {
                    CapacityBookingId = Guid.Empty,
                    Comments = "correction",
                }
            ),
            nameof(UpdateCapacityBookingRequest.CapacityBookingId)
        );
        AssertError(
            validator.Validate(EmptyUpdate() with { Version = 0, Comments = "correction" }),
            nameof(UpdateCapacityBookingRequest.Version)
        );
        AssertError(
            validator.Validate(EmptyUpdate() with { Version = -1, Comments = "correction" }),
            nameof(UpdateCapacityBookingRequest.Version)
        );
        AssertValid(
            validator.Validate(EmptyUpdate() with { Version = 1, Comments = "correction" })
        );
    }

    [Fact]
    public void UpdateRejectsAnEmptyPatchWithTheBusinessRuleMessage()
    {
        var result = new UpdateCapacityBookingValidator().Validate(EmptyUpdate());

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error =>
                string.Equals(
                    error.ErrorMessage,
                    "At least one mutable field is required.",
                    StringComparison.Ordinal
                )
        );
    }

    [Theory]
    [InlineData(nameof(UpdateCapacityBookingRequest.BalancingGroup))]
    [InlineData(nameof(UpdateCapacityBookingRequest.PriceMechanism))]
    [InlineData(nameof(UpdateCapacityBookingRequest.StartArea))]
    [InlineData(nameof(UpdateCapacityBookingRequest.EndArea))]
    [InlineData(nameof(UpdateCapacityBookingRequest.StartDay))]
    [InlineData(nameof(UpdateCapacityBookingRequest.EndDay))]
    [InlineData(nameof(UpdateCapacityBookingRequest.CapacityMw))]
    [InlineData(nameof(UpdateCapacityBookingRequest.CapacityPriceEurMwh))]
    [InlineData(nameof(UpdateCapacityBookingRequest.CapacityCostEur))]
    [InlineData(nameof(UpdateCapacityBookingRequest.Comments))]
    public void UpdateAcceptsEachMutableFieldWhenSuppliedAlone(string field)
    {
        var day = new DateOnly(2026, 1, 15);
        var request = field switch
        {
            nameof(UpdateCapacityBookingRequest.BalancingGroup) => EmptyUpdate() with
            {
                BalancingGroup = "NRGD",
            },
            nameof(UpdateCapacityBookingRequest.PriceMechanism) => EmptyUpdate() with
            {
                PriceMechanism = "GTF/THE - Yearly",
            },
            nameof(UpdateCapacityBookingRequest.StartArea) => EmptyUpdate() with
            {
                StartArea = "GTF",
            },
            nameof(UpdateCapacityBookingRequest.EndArea) => EmptyUpdate() with { EndArea = "THE" },
            nameof(UpdateCapacityBookingRequest.StartDay) => EmptyUpdate() with { StartDay = day },
            nameof(UpdateCapacityBookingRequest.EndDay) => EmptyUpdate() with { EndDay = day },
            nameof(UpdateCapacityBookingRequest.CapacityMw) => EmptyUpdate() with
            {
                CapacityMw = 0m,
            },
            nameof(UpdateCapacityBookingRequest.CapacityPriceEurMwh) => EmptyUpdate() with
            {
                CapacityPriceEurMwh = 0m,
            },
            nameof(UpdateCapacityBookingRequest.CapacityCostEur) => EmptyUpdate() with
            {
                CapacityCostEur = 0m,
            },
            nameof(UpdateCapacityBookingRequest.Comments) => EmptyUpdate() with
            {
                Comments = string.Empty,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
        };

        AssertValid(new UpdateCapacityBookingValidator().Validate(request));
    }
}
