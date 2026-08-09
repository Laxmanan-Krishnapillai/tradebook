using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record UpdateContractRequest
{
    public UpdateContractRequest() { }

    [SetsRequiredMembers]
    public UpdateContractRequest(
        ContractId ContractId,
        string ContractName,
        CounterpartyId CounterpartyId,
        string ProductType,
        string Action,
        string? CompanyShorthand,
        string? CountryCode,
        short? CountryDialCode,
        CompanyId? SourcingCenter,
        CompanyId? SalesCenter,
        string? BalancingGroup,
        string? GooQuality,
        string? SubsidyStatus,
        string? PriceMechanismGas,
        Price? FixedPriceGasEurMwh,
        string? ContractType,
        string? Comment,
        bool? IsActive,
        long Version
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
    }

    public required ContractId ContractId { get; init; }
    public required string ContractName { get; init; }
    public required CounterpartyId CounterpartyId { get; init; }
    public required string ProductType { get; init; }
    public required string Action { get; init; }

    [TsOptional]
    public string? CompanyShorthand { get; init; }

    [TsOptional]
    public string? CountryCode { get; init; }

    [TsOptional]
    public short? CountryDialCode { get; init; }

    [TsOptional]
    public CompanyId? SourcingCenter { get; init; }

    [TsOptional]
    public CompanyId? SalesCenter { get; init; }

    [TsOptional]
    public string? BalancingGroup { get; init; }

    [TsOptional]
    public string? GooQuality { get; init; }

    [TsOptional]
    public string? SubsidyStatus { get; init; }

    [TsOptional]
    public string? PriceMechanismGas { get; init; }

    [TsOptional]
    public Price? FixedPriceGasEurMwh { get; init; }

    [TsOptional]
    public string? ContractType { get; init; }

    [TsOptional]
    public string? Comment { get; init; }

    [TsOptional]
    public bool? IsActive { get; init; }
    public required long Version { get; init; }
}
