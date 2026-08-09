using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record ContractDetailsDto
{
    public ContractDetailsDto() { }

    [SetsRequiredMembers]
    public ContractDetailsDto(
        Guid ContractId,
        string ContractName,
        Guid CounterpartyId,
        string ProductType,
        string Action,
        string? CompanyShorthand,
        string? CountryCode,
        short? CountryDialCode,
        short? ContractNumber,
        short? YearOfContract,
        Guid? SourcingCenter,
        Guid? SalesCenter,
        string? BalancingGroup,
        string? GooQuality,
        string? SubsidyStatus,
        string? PriceMechanismGas,
        decimal? FixedPriceGasEurMwh,
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

    public required Guid ContractId { get; init; }
    public required string ContractName { get; init; }
    public required Guid CounterpartyId { get; init; }
    public required string ProductType { get; init; }
    public required string Action { get; init; }

    [TsOptional]
    public string? CompanyShorthand { get; init; }

    [TsOptional]
    public string? CountryCode { get; init; }

    [TsOptional]
    public short? CountryDialCode { get; init; }

    [TsOptional]
    public short? ContractNumber { get; init; }

    [TsOptional]
    public short? YearOfContract { get; init; }

    [TsOptional]
    public Guid? SourcingCenter { get; init; }

    [TsOptional]
    public Guid? SalesCenter { get; init; }

    [TsOptional]
    public string? BalancingGroup { get; init; }

    [TsOptional]
    public string? GooQuality { get; init; }

    [TsOptional]
    public string? SubsidyStatus { get; init; }

    [TsOptional]
    public string? PriceMechanismGas { get; init; }

    [TsOptional]
    public decimal? FixedPriceGasEurMwh { get; init; }
    public required string ContractType { get; init; }

    [TsOptional]
    public string? Comment { get; init; }
    public required bool IsActive { get; init; }
    public required long Version { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
