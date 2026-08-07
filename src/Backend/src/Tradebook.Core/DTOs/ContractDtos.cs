using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreateContractRequest(
    string ContractName,
    Guid CounterpartyId,
    string ProductType,
    string Action,
    [property: TsOptional] string? CompanyShorthand,
    [property: TsOptional] string? CountryCode,
    [property: TsOptional] short? CountryDialCode,
    [property: TsOptional] short? ContractNumber,
    [property: TsOptional] short? YearOfContract,
    [property: TsOptional] Guid? SourcingCenter,
    [property: TsOptional] Guid? SalesCenter,
    [property: TsOptional] string? BalancingGroup,
    [property: TsOptional] string? GooQuality,
    [property: TsOptional] string? SubsidyStatus,
    [property: TsOptional] string? PriceMechanismGas,
    [property: TsOptional] decimal? FixedPriceGasEurMwh,
    [property: TsOptional] string? ContractType,
    [property: TsOptional] string? Comment);

[ExportTsInterface]
public sealed record UpdateContractRequest(
    Guid ContractId,
    string ContractName,
    Guid CounterpartyId,
    string ProductType,
    string Action,
    [property: TsOptional] string? CompanyShorthand,
    [property: TsOptional] string? CountryCode,
    [property: TsOptional] short? CountryDialCode,
    [property: TsOptional] Guid? SourcingCenter,
    [property: TsOptional] Guid? SalesCenter,
    [property: TsOptional] string? BalancingGroup,
    [property: TsOptional] string? GooQuality,
    [property: TsOptional] string? SubsidyStatus,
    [property: TsOptional] string? PriceMechanismGas,
    [property: TsOptional] decimal? FixedPriceGasEurMwh,
    [property: TsOptional] string? ContractType,
    [property: TsOptional] string? Comment,
    [property: TsOptional] bool? IsActive,
    long Version);

[ExportTsInterface]
public sealed record ContractDetailsDto(
    Guid ContractId,
    string ContractName,
    Guid CounterpartyId,
    string ProductType,
    string Action,
    [property: TsOptional] string? CompanyShorthand,
    [property: TsOptional] string? CountryCode,
    [property: TsOptional] short? CountryDialCode,
    [property: TsOptional] short? ContractNumber,
    [property: TsOptional] short? YearOfContract,
    [property: TsOptional] Guid? SourcingCenter,
    [property: TsOptional] Guid? SalesCenter,
    [property: TsOptional] string? BalancingGroup,
    [property: TsOptional] string? GooQuality,
    [property: TsOptional] string? SubsidyStatus,
    [property: TsOptional] string? PriceMechanismGas,
    [property: TsOptional] decimal? FixedPriceGasEurMwh,
    string ContractType,
    [property: TsOptional] string? Comment,
    bool IsActive,
    long Version,
    DateTime CreatedAt,
    DateTime UpdatedAt);

[ExportTsInterface]
public sealed record GetContractHistoryRequest(
    [property: TsOptional] Guid? CounterpartyId,
    [property: TsOptional] string? ProductType,
    [property: TsOptional] string? Action,
    [property: TsOptional] bool? IsActive,
    int Page = 1,
    int PageSize = 50);

[ExportTsInterface]
public sealed record GetContractHistoryResponse(
    IReadOnlyList<ContractDetailsDto> Items, int TotalCount, int Page, int PageSize, bool HasNextPage);

[ExportTsInterface]
public sealed record DeactivateContractRequest(Guid ContractId, string Reason, long Version);
