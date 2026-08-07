using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreateGooCertificateTransactionRequest(
    [property: TsOptional] string? SalesforceTransactionId,
    [property: TsOptional] string? TransactionName,
    [property: TsOptional] string? BatchType,
    [property: TsOptional] string? CertificateTransactionId,
    [property: TsOptional] string? CountryOfProduction,
    [property: TsOptional] Guid? ProducerContractId,
    [property: TsOptional] string? ProducerCompany,
    [property: TsOptional] decimal? ProducerGooPriceEurMwh,
    [property: TsOptional] DateOnly? ProductionDate,
    [property: TsOptional] Guid? CustomerContractId,
    [property: TsOptional] string? CustomerCompany,
    [property: TsOptional] string? Register,
    [property: TsOptional] string? Status,
    [property: TsOptional] DateOnly? TransactionStartDate,
    [property: TsOptional] decimal? TransactionVolumeMwh,
    [property: TsOptional] decimal? VolumeMwh,
    [property: TsOptional] string? EnergySource,
    [property: TsOptional] string? Text);

[ExportTsInterface]
public sealed record UpdateGooCertificateTransactionRequest(
    Guid GooCertificateTransactionId,
    [property: TsOptional] string? BatchType,
    [property: TsOptional] Guid? ProducerContractId,
    [property: TsOptional] Guid? CustomerContractId,
    [property: TsOptional] string? Register,
    [property: TsOptional] string? Status,
    [property: TsOptional] DateOnly? TransactionStartDate,
    [property: TsOptional] decimal? TransactionVolumeMwh,
    [property: TsOptional] decimal? VolumeMwh,
    [property: TsOptional] string? Text,
    long Version);

[ExportTsInterface]
public sealed record GooCertificateTransactionDetailsDto(
    Guid GooCertificateTransactionId,
    [property: TsOptional] string? SalesforceTransactionId,
    [property: TsOptional] string? TransactionName,
    [property: TsOptional] string? BatchType,
    [property: TsOptional] string? CertificateTransactionId,
    [property: TsOptional] string? CountryOfProduction,
    [property: TsOptional] Guid? ProducerContractId,
    [property: TsOptional] string? ProducerCompany,
    [property: TsOptional] decimal? ProducerGooPriceEurMwh,
    [property: TsOptional] DateOnly? ProductionDate,
    [property: TsOptional] Guid? CustomerContractId,
    [property: TsOptional] string? CustomerCompany,
    [property: TsOptional] string? Register,
    [property: TsOptional] string? Status,
    [property: TsOptional] DateOnly? TransactionStartDate,
    [property: TsOptional] decimal? TransactionVolumeMwh,
    [property: TsOptional] decimal? VolumeMwh,
    [property: TsOptional] string? EnergySource,
    [property: TsOptional] string? Text,
    long Version, DateTime CreatedAt, DateTime UpdatedAt);

[ExportTsInterface]
public sealed record GetGooCertificateHistoryRequest(
    [property: TsOptional] Guid? ContractId,
    [property: TsOptional] string? Status,
    [property: TsOptional] DateOnly? FromDate,
    [property: TsOptional] DateOnly? ToDate,
    int Page = 1,
    int PageSize = 50);

[ExportTsInterface]
public sealed record GetGooCertificateHistoryResponse(
    IReadOnlyList<GooCertificateTransactionDetailsDto> Items,
    int TotalCount, int Page, int PageSize, bool HasNextPage);

[ExportTsInterface]
public sealed record RequestGooBatchExportRequest(Guid GooCertificateTransactionId, long Version);

[ExportTsInterface]
public sealed record DeleteGooCertificateTransactionRequest(
    Guid GooCertificateTransactionId, string Reason, long Version);
