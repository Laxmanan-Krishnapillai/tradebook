using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;

namespace Tradebook.Core.DTOs;

public sealed record CreateContractRequest
{
    public CreateContractRequest() { }

    [SetsRequiredMembers]
    public CreateContractRequest(
        string ContractName,
        CounterpartyId CounterpartyId,
        string ProductType,
        string Action,
        string? CompanyShorthand,
        string? CountryCode,
        short? CountryDialCode,
        short? ContractNumber,
        short? YearOfContract,
        CompanyId? SourcingCenter,
        CompanyId? SalesCenter,
        string? BalancingGroup,
        string? GooQuality,
        string? SubsidyStatus,
        string? PriceMechanismGas,
        Price? FixedPriceGasEurMwh,
        string? ContractType,
        string? Comment
    )
    {
        this.ContractName = ContractName;
        this.CounterpartyId = CounterpartyId;
        this.ProductType = ProductType;
        this.Action = Action;
        this.CompanyShorthand = CompanyShorthand;
        this.CountryCode = CountryCode;
        this.CountryDialCode = CountryDialCode;
        this.ContractNumber = ContractNumber;
        this.YearOfContract = YearOfContract;
        this.SourcingCenter = SourcingCenter;
        this.SalesCenter = SalesCenter;
        this.BalancingGroup = BalancingGroup;
        this.GooQuality = GooQuality;
        this.SubsidyStatus = SubsidyStatus;
        this.PriceMechanismGas = PriceMechanismGas;
        this.FixedPriceGasEurMwh = FixedPriceGasEurMwh;
        this.ContractType = ContractType;
        this.Comment = Comment;
    }

    public required string ContractName { get; init; }
    public required CounterpartyId CounterpartyId { get; init; }
    public required string ProductType { get; init; }
    public required string Action { get; init; }

    public string? CompanyShorthand { get; init; }

    public string? CountryCode { get; init; }

    public short? CountryDialCode { get; init; }

    public short? ContractNumber { get; init; }

    public short? YearOfContract { get; init; }

    public CompanyId? SourcingCenter { get; init; }

    public CompanyId? SalesCenter { get; init; }

    public string? BalancingGroup { get; init; }

    public string? GooQuality { get; init; }

    public string? SubsidyStatus { get; init; }

    public string? PriceMechanismGas { get; init; }

    public Price? FixedPriceGasEurMwh { get; init; }

    public string? ContractType { get; init; }

    public string? Comment { get; init; }
}
