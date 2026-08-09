using FluentValidation.Results;
using Tradebook.Api.Features.Contracts;
using Tradebook.Core.DTOs;

namespace Tradebook.UnitTests;

public sealed class ContractValidatorTests
{
    private static CreateContractRequest ValidCreate() =>
        new(
            ContractName: "ARLA45.SC.2601.ETSS",
            CounterpartyId: Guid.NewGuid(),
            ProductType: "GoO",
            Action: "Sell",
            CompanyShorthand: "ARLA",
            CountryCode: "DK",
            CountryDialCode: 45,
            ContractNumber: 1,
            YearOfContract: 2026,
            SourcingCenter: null,
            SalesCenter: null,
            BalancingGroup: null,
            GooQuality: "ETS",
            SubsidyStatus: "SUB",
            PriceMechanismGas: "TTF",
            FixedPriceGasEurMwh: 0m,
            ContractType: "External",
            Comment: null
        );

    private static UpdateContractRequest ValidUpdate() =>
        new(
            ContractId: Guid.NewGuid(),
            ContractName: "ARLA45.SC.2601.ETSS",
            CounterpartyId: Guid.NewGuid(),
            ProductType: "GoO",
            Action: "Sell",
            CompanyShorthand: "ARLA",
            CountryCode: "DK",
            CountryDialCode: 45,
            SourcingCenter: null,
            SalesCenter: null,
            BalancingGroup: null,
            GooQuality: "ETS",
            SubsidyStatus: "SUB",
            PriceMechanismGas: "TTF",
            FixedPriceGasEurMwh: 0m,
            ContractType: "External",
            Comment: null,
            IsActive: true,
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
    public void ValidCreateAndUpdateRequestsPass()
    {
        AssertValid(new CreateContractValidator().Validate(ValidCreate()));
        AssertValid(new UpdateContractValidator().Validate(ValidUpdate()));
    }

    [Fact]
    public void ContractNameIsRequiredAndHonorsTheExact100CharacterBoundary()
    {
        var createValidator = new CreateContractValidator();
        var updateValidator = new UpdateContractValidator();

        foreach (var invalidName in new[] { null, string.Empty, "   " })
        {
            AssertError(
                createValidator.Validate(ValidCreate() with { ContractName = invalidName! }),
                nameof(CreateContractRequest.ContractName)
            );
            AssertError(
                updateValidator.Validate(ValidUpdate() with { ContractName = invalidName! }),
                nameof(UpdateContractRequest.ContractName)
            );
        }

        AssertValid(
            createValidator.Validate(ValidCreate() with { ContractName = new string('x', 100) })
        );
        AssertValid(
            updateValidator.Validate(ValidUpdate() with { ContractName = new string('x', 100) })
        );
        AssertError(
            createValidator.Validate(ValidCreate() with { ContractName = new string('x', 101) }),
            nameof(CreateContractRequest.ContractName)
        );
        AssertError(
            updateValidator.Validate(ValidUpdate() with { ContractName = new string('x', 101) }),
            nameof(UpdateContractRequest.ContractName)
        );
    }

    [Fact]
    public void CounterpartyIsRequiredForCreateAndUpdate()
    {
        AssertError(
            new CreateContractValidator().Validate(
                ValidCreate() with
                {
                    CounterpartyId = Guid.Empty,
                }
            ),
            nameof(CreateContractRequest.CounterpartyId)
        );
        AssertError(
            new UpdateContractValidator().Validate(
                ValidUpdate() with
                {
                    CounterpartyId = Guid.Empty,
                }
            ),
            nameof(UpdateContractRequest.CounterpartyId)
        );
    }

    [Theory]
    [InlineData("GoO")]
    [InlineData("Gas")]
    [InlineData("GoO+Gas")]
    [InlineData("GoO+Gas+Shipping")]
    [InlineData("Tickets")]
    public void CreateAndUpdateAcceptEveryAuthoritativeProductType(string productType)
    {
        AssertValid(
            new CreateContractValidator().Validate(ValidCreate() with { ProductType = productType })
        );
        AssertValid(
            new UpdateContractValidator().Validate(ValidUpdate() with { ProductType = productType })
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("goo")]
    [InlineData("Electricity")]
    public void CreateAndUpdateRejectUnknownOrNoncanonicalProductTypes(string productType)
    {
        AssertError(
            new CreateContractValidator().Validate(
                ValidCreate() with
                {
                    ProductType = productType,
                }
            ),
            nameof(CreateContractRequest.ProductType)
        );
        AssertError(
            new UpdateContractValidator().Validate(
                ValidUpdate() with
                {
                    ProductType = productType,
                }
            ),
            nameof(UpdateContractRequest.ProductType)
        );
    }

    [Theory]
    [InlineData("Buy")]
    [InlineData("Sell")]
    [InlineData("Intercompany")]
    [InlineData("Swap")]
    public void CreateAndUpdateAcceptEveryAuthoritativeAction(string action)
    {
        AssertValid(new CreateContractValidator().Validate(ValidCreate() with { Action = action }));
        AssertValid(new UpdateContractValidator().Validate(ValidUpdate() with { Action = action }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("buy")]
    [InlineData("Hold")]
    public void CreateAndUpdateRejectUnknownOrNoncanonicalActions(string action)
    {
        AssertError(
            new CreateContractValidator().Validate(ValidCreate() with { Action = action }),
            nameof(CreateContractRequest.Action)
        );
        AssertError(
            new UpdateContractValidator().Validate(ValidUpdate() with { Action = action }),
            nameof(UpdateContractRequest.Action)
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("RED")]
    [InlineData("ETS")]
    [InlineData("OZD")]
    [InlineData("NMS")]
    [InlineData("EWG")]
    [InlineData("ISCC")]
    [InlineData("NOQ")]
    [InlineData("GEG")]
    [InlineData("RTFO")]
    [InlineData("BHG")]
    public void CreateAndUpdateAcceptEveryAuthoritativeGooQuality(string? quality)
    {
        AssertValid(
            new CreateContractValidator().Validate(ValidCreate() with { GooQuality = quality })
        );
        AssertValid(
            new UpdateContractValidator().Validate(ValidUpdate() with { GooQuality = quality })
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("red")]
    [InlineData("CO2E")]
    public void CreateAndUpdateRejectUnknownOrNoncanonicalGooQualities(string quality)
    {
        AssertError(
            new CreateContractValidator().Validate(ValidCreate() with { GooQuality = quality }),
            nameof(CreateContractRequest.GooQuality)
        );
        AssertError(
            new UpdateContractValidator().Validate(ValidUpdate() with { GooQuality = quality }),
            nameof(UpdateContractRequest.GooQuality)
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("SUB")]
    [InlineData("UNS")]
    [InlineData("None")]
    public void CreateAndUpdateAcceptEveryAuthoritativeSubsidyStatus(string? subsidyStatus)
    {
        AssertValid(
            new CreateContractValidator().Validate(
                ValidCreate() with
                {
                    SubsidyStatus = subsidyStatus,
                }
            )
        );
        AssertValid(
            new UpdateContractValidator().Validate(
                ValidUpdate() with
                {
                    SubsidyStatus = subsidyStatus,
                }
            )
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("sub")]
    [InlineData("NONE")]
    public void CreateAndUpdateRejectUnknownOrNoncanonicalSubsidyStatuses(string subsidyStatus)
    {
        AssertError(
            new CreateContractValidator().Validate(
                ValidCreate() with
                {
                    SubsidyStatus = subsidyStatus,
                }
            ),
            nameof(CreateContractRequest.SubsidyStatus)
        );
        AssertError(
            new UpdateContractValidator().Validate(
                ValidUpdate() with
                {
                    SubsidyStatus = subsidyStatus,
                }
            ),
            nameof(UpdateContractRequest.SubsidyStatus)
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("FIXED")]
    [InlineData("VARIABLE")]
    [InlineData("EGSI ETF")]
    [InlineData("TTF")]
    [InlineData("WITHIN-DAY MKT")]
    [InlineData("BGO")]
    [InlineData("PGO")]
    [InlineData("THE")]
    public void CreateAndUpdateAcceptEveryAuthoritativeGasPriceMechanism(string? priceMechanism)
    {
        AssertValid(
            new CreateContractValidator().Validate(
                ValidCreate() with
                {
                    PriceMechanismGas = priceMechanism,
                }
            )
        );
        AssertValid(
            new UpdateContractValidator().Validate(
                ValidUpdate() with
                {
                    PriceMechanismGas = priceMechanism,
                }
            )
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("Fixed")]
    [InlineData("SPOT")]
    public void CreateAndUpdateRejectUnknownOrNoncanonicalGasPriceMechanisms(string priceMechanism)
    {
        AssertError(
            new CreateContractValidator().Validate(
                ValidCreate() with
                {
                    PriceMechanismGas = priceMechanism,
                }
            ),
            nameof(CreateContractRequest.PriceMechanismGas)
        );
        AssertError(
            new UpdateContractValidator().Validate(
                ValidUpdate() with
                {
                    PriceMechanismGas = priceMechanism,
                }
            ),
            nameof(UpdateContractRequest.PriceMechanismGas)
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("External")]
    [InlineData("Intercompany")]
    public void CreateAndUpdateAcceptEveryAuthoritativeContractType(string? contractType)
    {
        AssertValid(
            new CreateContractValidator().Validate(
                ValidCreate() with
                {
                    ContractType = contractType,
                }
            )
        );
        AssertValid(
            new UpdateContractValidator().Validate(
                ValidUpdate() with
                {
                    ContractType = contractType,
                }
            )
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("external")]
    [InlineData("Internal")]
    public void CreateAndUpdateRejectUnknownOrNoncanonicalContractTypes(string contractType)
    {
        AssertError(
            new CreateContractValidator().Validate(
                ValidCreate() with
                {
                    ContractType = contractType,
                }
            ),
            nameof(CreateContractRequest.ContractType)
        );
        AssertError(
            new UpdateContractValidator().Validate(
                ValidUpdate() with
                {
                    ContractType = contractType,
                }
            ),
            nameof(UpdateContractRequest.ContractType)
        );
    }

    [Fact]
    public void UpdateRequiresIdentityAndAPositiveVersion()
    {
        var validator = new UpdateContractValidator();

        AssertError(
            validator.Validate(ValidUpdate() with { ContractId = Guid.Empty }),
            nameof(UpdateContractRequest.ContractId)
        );
        AssertError(
            validator.Validate(ValidUpdate() with { Version = 0 }),
            nameof(UpdateContractRequest.Version)
        );
        AssertError(
            validator.Validate(ValidUpdate() with { Version = -1 }),
            nameof(UpdateContractRequest.Version)
        );
        AssertValid(validator.Validate(ValidUpdate() with { Version = 1 }));
    }
}
