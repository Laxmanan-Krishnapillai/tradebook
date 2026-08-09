using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreateGooCertificateTransactionRequest(
    [property: TsOptional] string? SalesforceTransactionId,
    [property: TsOptional] string? TransactionName,
    [property: TsOptional] string? BatchType,
    [property: TsOptional] string? CertificateTransactionId,
    [property: TsOptional] string? CountryOfProduction,
    [property: TsOptional] ContractId? ProducerContractId,
    [property: TsOptional] string? ProducerCompany,
    [property: TsOptional] Price? ProducerGooPriceEurMwh,
    [property: TsOptional] DateOnly? ProductionDate,
    [property: TsOptional] ContractId? CustomerContractId,
    [property: TsOptional] string? CustomerCompany,
    [property: TsOptional] string? Register,
    [property: TsOptional] string? Status,
    [property: TsOptional] DateOnly? TransactionStartDate,
    [property: TsOptional] Quantity? TransactionVolumeMwh,
    [property: TsOptional] Quantity? VolumeMwh,
    [property: TsOptional] string? EnergySource,
    [property: TsOptional] string? Text);

[ExportTsInterface]
public sealed record UpdateGooCertificateTransactionRequest(
    GooCertificateTransactionId GooCertificateTransactionId,
    [property: TsOptional] string? BatchType,
    [property: TsOptional] ContractId? ProducerContractId,
    [property: TsOptional] ContractId? CustomerContractId,
    [property: TsOptional] string? Register,
    [property: TsOptional] string? Status,
    [property: TsOptional] DateOnly? TransactionStartDate,
    [property: TsOptional] Quantity? TransactionVolumeMwh,
    [property: TsOptional] Quantity? VolumeMwh,
    [property: TsOptional] string? Text,
    long Version);

[ExportTsInterface]
public sealed record GooCertificateTransactionDetailsDto(
    GooCertificateTransactionId GooCertificateTransactionId,
    [property: TsOptional] string? SalesforceTransactionId,
    [property: TsOptional] string? TransactionName,
    [property: TsOptional] string? BatchType,
    [property: TsOptional] string? CertificateTransactionId,
    [property: TsOptional] string? CountryOfProduction,
    [property: TsOptional] ContractId? ProducerContractId,
    [property: TsOptional] string? ProducerCompany,
    [property: TsOptional] Price? ProducerGooPriceEurMwh,
    [property: TsOptional] DateOnly? ProductionDate,
    [property: TsOptional] ContractId? CustomerContractId,
    [property: TsOptional] string? CustomerCompany,
    [property: TsOptional] string? Register,
    [property: TsOptional] string? Status,
    [property: TsOptional] DateOnly? TransactionStartDate,
    [property: TsOptional] Quantity? TransactionVolumeMwh,
    [property: TsOptional] Quantity? VolumeMwh,
    [property: TsOptional] string? EnergySource,
    [property: TsOptional] string? Text,
    long Version, DateTime CreatedAt, DateTime UpdatedAt);

[ExportTsInterface]
public sealed record GetGooCertificateHistoryRequest(
    [property: TsOptional] ContractId? ContractId,
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
public sealed record RequestGooBatchExportRequest(GooCertificateTransactionId GooCertificateTransactionId, long Version);

[ExportTsInterface]
public sealed record DeleteGooCertificateTransactionRequest(
    GooCertificateTransactionId GooCertificateTransactionId, string Reason, long Version);
