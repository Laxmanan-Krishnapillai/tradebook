using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;

namespace Tradebook.Core.DTOs;

public sealed record ContractDetailsDto
{
    public ContractDetailsDto() { }

    [SetsRequiredMembers]
    public ContractDetailsDto(
        ContractId ContractId,
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
        string ContractType,
        string? Comment,
        bool IsActive,
        long Version,
        DateTime CreatedAt,
        DateTime UpdatedAt
    )
    {
        this.ContractId = ContractId;
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
        this.IsActive = IsActive;
        this.Version = Version;
        this.CreatedAt = CreatedAt;
        this.UpdatedAt = UpdatedAt;
    }

    public required ContractId ContractId { get; init; }
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
    public required string ContractType { get; init; }

    public string? Comment { get; init; }
    public required bool IsActive { get; init; }
    public required long Version { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
