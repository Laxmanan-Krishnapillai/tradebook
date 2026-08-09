using FluentValidation.Results;
using Tradebook.Api.Features.Biotickets;
using Tradebook.Core.DTOs;

namespace Tradebook.UnitTests;

public sealed class BioticketValidatorTests
{
    private static CreateBioticketRequest ValidCreate() =>
        new(
            ContractId: Guid.NewGuid(),
            BookType: "Sourcing",
            ContractMonth: new DateOnly(2026, 1, 1),
            ContractInstanceId: null,
            StartDay: new DateOnly(2026, 1, 1),
            EndDay: new DateOnly(2026, 1, 31),
            VolumeNominatedTon: 12m,
            VolumeRealisedTon: 11m,
            VolumeTon: 10m,
            CostEurTon: 20m,
            RevenueEur: 200m,
            VatPct: 0.25m,
            VatEur: 50m,
            InvoiceAmountEur: 250m,
            Status: "Awaiting",
            Comment: null
        );

    private static UpdateBioticketRequest EmptyUpdate() =>
        new(
            BioticketId: Guid.NewGuid(),
            VolumeRealisedTon: null,
            VolumeTon: null,
            CostEurTon: null,
            RevenueEur: null,
            VatPct: null,
            VatEur: null,
            InvoiceAmountEur: null,
            Status: null,
            Comment: null,
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
        AssertValid(new CreateBioticketValidator().Validate(ValidCreate()));

    [Theory]
    [InlineData("Sourcing")]
    [InlineData("Sales")]
    public void CreateAcceptsEveryAuthoritativeBookType(string bookType) =>
        AssertValid(
            new CreateBioticketValidator().Validate(ValidCreate() with { BookType = bookType })
        );

    [Theory]
    [InlineData("")]
    [InlineData("Intercompany")]
    [InlineData("sales")]
    public void CreateRejectsUnknownOrNoncanonicalBookTypes(string bookType) =>
        AssertError(
            new CreateBioticketValidator().Validate(ValidCreate() with { BookType = bookType }),
            nameof(CreateBioticketRequest.BookType)
        );

    [Fact]
    public void CreateRequiresContractIdentityAndAFirstOfMonthContractMonth()
    {
        var validator = new CreateBioticketValidator();

        AssertError(
            validator.Validate(ValidCreate() with { ContractId = Guid.Empty }),
            nameof(CreateBioticketRequest.ContractId)
        );
        AssertError(
            validator.Validate(ValidCreate() with { ContractMonth = default }),
            nameof(CreateBioticketRequest.ContractMonth)
        );
        AssertError(
            validator.Validate(ValidCreate() with { ContractMonth = new DateOnly(2026, 1, 2) }),
            nameof(CreateBioticketRequest.ContractMonth)
        );
    }

    [Fact]
    public void CreateDateRangeAcceptsPartialAndEqualBoundsButRejectsReverseOrder()
    {
        var validator = new CreateBioticketValidator();
        var day = new DateOnly(2026, 1, 15);

        AssertValid(validator.Validate(ValidCreate() with { StartDay = day, EndDay = day }));
        AssertValid(validator.Validate(ValidCreate() with { StartDay = day, EndDay = null }));
        AssertValid(validator.Validate(ValidCreate() with { StartDay = null, EndDay = day }));
        AssertError(
            validator.Validate(ValidCreate() with { StartDay = day, EndDay = day.AddDays(-1) }),
            nameof(CreateBioticketRequest.EndDay)
        );
    }

    [Fact]
    public void CreateTonneAmountsAcceptZeroAndRejectNegativeValuesIndependently()
    {
        var validator = new CreateBioticketValidator();
        AssertValid(
            validator.Validate(
                ValidCreate() with
                {
                    VolumeNominatedTon = 0m,
                    VolumeRealisedTon = 0m,
                    VolumeTon = 0m,
                }
            )
        );

        var invalidCases = new[]
        {
            (
                Request: ValidCreate() with
                {
                    VolumeNominatedTon = -0.01m,
                },
                Property: nameof(CreateBioticketRequest.VolumeNominatedTon)
            ),
            (
                Request: ValidCreate() with
                {
                    VolumeRealisedTon = -0.01m,
                },
                Property: nameof(CreateBioticketRequest.VolumeRealisedTon)
            ),
            (
                Request: ValidCreate() with
                {
                    VolumeTon = -0.01m,
                },
                Property: nameof(CreateBioticketRequest.VolumeTon)
            ),
        };

        foreach (var (request, property) in invalidCases)
        {
            AssertError(validator.Validate(request), property);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Completed - Payment Received/Sent")]
    [InlineData("In Progress - Invoice Received/Sent")]
    [InlineData("Pending - No Invoice")]
    [InlineData("Cancelled")]
    [InlineData("Awaiting")]
    [InlineData("Issue")]
    public void CreateAndUpdateAcceptEveryAuthoritativeReportStatus(string? status)
    {
        AssertValid(
            new CreateBioticketValidator().Validate(ValidCreate() with { Status = status })
        );
        AssertValid(
            new UpdateBioticketValidator().Validate(
                EmptyUpdate() with
                {
                    VolumeTon = 0m,
                    Status = status,
                }
            )
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("Deleted")]
    [InlineData("awaiting")]
    public void CreateAndUpdateRejectUnknownOrNoncanonicalReportStatuses(string status)
    {
        AssertError(
            new CreateBioticketValidator().Validate(ValidCreate() with { Status = status }),
            nameof(CreateBioticketRequest.Status)
        );
        AssertError(
            new UpdateBioticketValidator().Validate(EmptyUpdate() with { Status = status }),
            nameof(UpdateBioticketRequest.Status)
        );
    }

    [Fact]
    public void UpdateRequiresIdentityAndAPositiveVersion()
    {
        var validator = new UpdateBioticketValidator();

        AssertError(
            validator.Validate(
                EmptyUpdate() with
                {
                    BioticketId = Guid.Empty,
                    Comment = "correction",
                }
            ),
            nameof(UpdateBioticketRequest.BioticketId)
        );
        AssertError(
            validator.Validate(EmptyUpdate() with { Version = 0, Comment = "correction" }),
            nameof(UpdateBioticketRequest.Version)
        );
        AssertError(
            validator.Validate(EmptyUpdate() with { Version = -1, Comment = "correction" }),
            nameof(UpdateBioticketRequest.Version)
        );
        AssertValid(validator.Validate(EmptyUpdate() with { Version = 1, Comment = "correction" }));
    }

    [Fact]
    public void UpdateTonneAmountsAcceptZeroAndRejectNegativeValuesIndependently()
    {
        var validator = new UpdateBioticketValidator();

        AssertValid(validator.Validate(EmptyUpdate() with { VolumeRealisedTon = 0m }));
        AssertValid(validator.Validate(EmptyUpdate() with { VolumeTon = 0m }));
        AssertError(
            validator.Validate(EmptyUpdate() with { VolumeRealisedTon = -0.01m }),
            nameof(UpdateBioticketRequest.VolumeRealisedTon)
        );
        AssertError(
            validator.Validate(EmptyUpdate() with { VolumeTon = -0.01m }),
            nameof(UpdateBioticketRequest.VolumeTon)
        );
    }

    [Fact]
    public void UpdateRejectsAnEmptyPatchWithTheBusinessRuleMessage()
    {
        var result = new UpdateBioticketValidator().Validate(EmptyUpdate());

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
    [InlineData(nameof(UpdateBioticketRequest.VolumeRealisedTon))]
    [InlineData(nameof(UpdateBioticketRequest.VolumeTon))]
    [InlineData(nameof(UpdateBioticketRequest.CostEurTon))]
    [InlineData(nameof(UpdateBioticketRequest.RevenueEur))]
    [InlineData(nameof(UpdateBioticketRequest.VatPct))]
    [InlineData(nameof(UpdateBioticketRequest.VatEur))]
    [InlineData(nameof(UpdateBioticketRequest.InvoiceAmountEur))]
    [InlineData(nameof(UpdateBioticketRequest.Status))]
    [InlineData(nameof(UpdateBioticketRequest.Comment))]
    public void UpdateAcceptsEachMutableFieldWhenSuppliedAlone(string field)
    {
        var request = field switch
        {
            nameof(UpdateBioticketRequest.VolumeRealisedTon) => EmptyUpdate() with
            {
                VolumeRealisedTon = 0m,
            },
            nameof(UpdateBioticketRequest.VolumeTon) => EmptyUpdate() with { VolumeTon = 0m },
            nameof(UpdateBioticketRequest.CostEurTon) => EmptyUpdate() with { CostEurTon = 0m },
            nameof(UpdateBioticketRequest.RevenueEur) => EmptyUpdate() with { RevenueEur = 0m },
            nameof(UpdateBioticketRequest.VatPct) => EmptyUpdate() with { VatPct = 0m },
            nameof(UpdateBioticketRequest.VatEur) => EmptyUpdate() with { VatEur = 0m },
            nameof(UpdateBioticketRequest.InvoiceAmountEur) => EmptyUpdate() with
            {
                InvoiceAmountEur = 0m,
            },
            nameof(UpdateBioticketRequest.Status) => EmptyUpdate() with { Status = "Awaiting" },
            nameof(UpdateBioticketRequest.Comment) => EmptyUpdate() with { Comment = string.Empty },
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
        };

        AssertValid(new UpdateBioticketValidator().Validate(request));
    }
}
