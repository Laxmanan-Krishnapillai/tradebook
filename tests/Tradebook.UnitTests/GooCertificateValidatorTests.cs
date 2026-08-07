using FluentValidation.Results;
using Tradebook.Api.Features.GooCertificates;
using Tradebook.Core.DTOs;

namespace Tradebook.UnitTests;

public sealed class GooCertificateValidatorTests
{
    private static CreateGooCertificateTransactionRequest ValidCreate() => new(
        SalesforceTransactionId: "a07TG00000PMLtSYAX",
        TransactionName: "7265-17552",
        BatchType: "Dena-Internal transaction",
        CertificateTransactionId: "847513",
        CountryOfProduction: "NL",
        ProducerContractId: Guid.NewGuid(),
        ProducerCompany: "Producer",
        ProducerGooPriceEurMwh: 0m,
        ProductionDate: new DateOnly(2026, 1, 1),
        CustomerContractId: null,
        CustomerCompany: null,
        Register: "Dena",
        Status: "Processing",
        TransactionStartDate: new DateOnly(2026, 1, 2),
        TransactionVolumeMwh: 0m,
        VolumeMwh: 0m,
        EnergySource: "Biogas",
        Text: null);

    private static UpdateGooCertificateTransactionRequest EmptyUpdate() => new(
        GooCertificateTransactionId: Guid.NewGuid(),
        BatchType: null,
        ProducerContractId: null,
        CustomerContractId: null,
        Register: null,
        Status: null,
        TransactionStartDate: null,
        TransactionVolumeMwh: null,
        VolumeMwh: null,
        Text: null,
        Version: 1);

    private static void AssertValid(ValidationResult result) =>
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors.Select(error => error.ErrorMessage)));

    private static void AssertError(ValidationResult result, string propertyName)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == propertyName);
    }

    [Fact]
    public void Valid_create_request_passes() =>
        AssertValid(new CreateGooCertificateValidator().Validate(ValidCreate()));

    [Theory]
    [InlineData(null)]
    [InlineData("Latest transaction")]
    [InlineData("Batch export requested")]
    [InlineData("Processing")]
    [InlineData("Completed")]
    [InlineData("Failed")]
    public void Create_and_update_accept_every_authoritative_transaction_status(string? status)
    {
        AssertValid(new CreateGooCertificateValidator().Validate(ValidCreate() with { Status = status }));
        AssertValid(new UpdateGooCertificateValidator().Validate(EmptyUpdate() with { Text = "correction", Status = status }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Awaiting")]
    [InlineData("processing")]
    public void Create_and_update_reject_unknown_or_noncanonical_transaction_statuses(string status)
    {
        AssertError(
            new CreateGooCertificateValidator().Validate(ValidCreate() with { Status = status }),
            nameof(CreateGooCertificateTransactionRequest.Status));
        AssertError(
            new UpdateGooCertificateValidator().Validate(EmptyUpdate() with { Status = status }),
            nameof(UpdateGooCertificateTransactionRequest.Status));
    }

    [Fact]
    public void Country_of_production_is_optional_but_must_be_exactly_two_characters_when_present()
    {
        var validator = new CreateGooCertificateValidator();

        AssertValid(validator.Validate(ValidCreate() with { CountryOfProduction = null }));
        AssertValid(validator.Validate(ValidCreate() with { CountryOfProduction = "DK" }));
        foreach (var invalidCountry in new[] { string.Empty, "D", "DNK" })
        {
            AssertError(
                validator.Validate(ValidCreate() with { CountryOfProduction = invalidCountry }),
                nameof(CreateGooCertificateTransactionRequest.CountryOfProduction));
        }
    }

    [Fact]
    public void Create_requires_at_least_one_contract_leg_and_accepts_either_leg_independently()
    {
        var validator = new CreateGooCertificateValidator();
        var producerId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var missing = validator.Validate(ValidCreate() with
        {
            ProducerContractId = null,
            CustomerContractId = null
        });
        Assert.False(missing.IsValid);
        Assert.Contains(missing.Errors,
            error => error.ErrorMessage == "At least one producer or customer contract is required.");

        AssertValid(validator.Validate(ValidCreate() with
        {
            ProducerContractId = producerId,
            CustomerContractId = null
        }));
        AssertValid(validator.Validate(ValidCreate() with
        {
            ProducerContractId = null,
            CustomerContractId = customerId
        }));
        AssertValid(validator.Validate(ValidCreate() with
        {
            ProducerContractId = producerId,
            CustomerContractId = customerId
        }));
    }

    [Fact]
    public void Update_requires_identity_and_a_positive_version()
    {
        var validator = new UpdateGooCertificateValidator();

        AssertError(
            validator.Validate(EmptyUpdate() with { GooCertificateTransactionId = Guid.Empty, Text = "correction" }),
            nameof(UpdateGooCertificateTransactionRequest.GooCertificateTransactionId));
        AssertError(
            validator.Validate(EmptyUpdate() with { Version = 0, Text = "correction" }),
            nameof(UpdateGooCertificateTransactionRequest.Version));
        AssertError(
            validator.Validate(EmptyUpdate() with { Version = -1, Text = "correction" }),
            nameof(UpdateGooCertificateTransactionRequest.Version));
        AssertValid(validator.Validate(EmptyUpdate() with { Version = 1, Text = "correction" }));
    }

    [Fact]
    public void Update_rejects_an_empty_patch_with_the_business_rule_message()
    {
        var result = new UpdateGooCertificateValidator().Validate(EmptyUpdate());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage == "At least one mutable field is required.");
    }

    [Theory]
    [InlineData(nameof(UpdateGooCertificateTransactionRequest.BatchType))]
    [InlineData(nameof(UpdateGooCertificateTransactionRequest.ProducerContractId))]
    [InlineData(nameof(UpdateGooCertificateTransactionRequest.CustomerContractId))]
    [InlineData(nameof(UpdateGooCertificateTransactionRequest.Register))]
    [InlineData(nameof(UpdateGooCertificateTransactionRequest.Status))]
    [InlineData(nameof(UpdateGooCertificateTransactionRequest.TransactionStartDate))]
    [InlineData(nameof(UpdateGooCertificateTransactionRequest.TransactionVolumeMwh))]
    [InlineData(nameof(UpdateGooCertificateTransactionRequest.VolumeMwh))]
    [InlineData(nameof(UpdateGooCertificateTransactionRequest.Text))]
    public void Update_accepts_each_mutable_field_when_supplied_alone(string field)
    {
        var request = field switch
        {
            nameof(UpdateGooCertificateTransactionRequest.BatchType) => EmptyUpdate() with { BatchType = "Dena-Internal transaction" },
            nameof(UpdateGooCertificateTransactionRequest.ProducerContractId) => EmptyUpdate() with { ProducerContractId = Guid.NewGuid() },
            nameof(UpdateGooCertificateTransactionRequest.CustomerContractId) => EmptyUpdate() with { CustomerContractId = Guid.NewGuid() },
            nameof(UpdateGooCertificateTransactionRequest.Register) => EmptyUpdate() with { Register = "Dena" },
            nameof(UpdateGooCertificateTransactionRequest.Status) => EmptyUpdate() with { Status = "Processing" },
            nameof(UpdateGooCertificateTransactionRequest.TransactionStartDate) => EmptyUpdate() with { TransactionStartDate = new DateOnly(2026, 1, 1) },
            nameof(UpdateGooCertificateTransactionRequest.TransactionVolumeMwh) => EmptyUpdate() with { TransactionVolumeMwh = 0m },
            nameof(UpdateGooCertificateTransactionRequest.VolumeMwh) => EmptyUpdate() with { VolumeMwh = 0m },
            nameof(UpdateGooCertificateTransactionRequest.Text) => EmptyUpdate() with { Text = string.Empty },
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
        };

        AssertValid(new UpdateGooCertificateValidator().Validate(request));
    }
}
