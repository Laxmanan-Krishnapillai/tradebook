using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record GooCertificateTransactionDetailsDto
{
    public GooCertificateTransactionDetailsDto() { }

    [SetsRequiredMembers]
    public GooCertificateTransactionDetailsDto(
        GooCertificateTransactionId GooCertificateTransactionId,
        string? SalesforceTransactionId,
        string? TransactionName,
        string? BatchType,
        string? CertificateTransactionId,
        string? CountryOfProduction,
        ContractId? ProducerContractId,
        string? ProducerCompany,
        Price? ProducerGooPriceEurMwh,
        DateOnly? ProductionDate,
        ContractId? CustomerContractId,
        string? CustomerCompany,
        string? Register,
        string? Status,
        DateOnly? TransactionStartDate,
        Quantity? TransactionVolumeMwh,
        Quantity? VolumeMwh,
        string? EnergySource,
        string? Text,
        long Version,
        DateTime CreatedAt,
        DateTime UpdatedAt
    )
    {
        this.GooCertificateTransactionId = GooCertificateTransactionId;
        this.SalesforceTransactionId = SalesforceTransactionId;
        this.TransactionName = TransactionName;
        this.BatchType = BatchType;
        this.CertificateTransactionId = CertificateTransactionId;
        this.CountryOfProduction = CountryOfProduction;
        this.ProducerContractId = ProducerContractId;
        this.ProducerCompany = ProducerCompany;
        this.ProducerGooPriceEurMwh = ProducerGooPriceEurMwh;
        this.ProductionDate = ProductionDate;
        this.CustomerContractId = CustomerContractId;
        this.CustomerCompany = CustomerCompany;
        this.Register = Register;
        this.Status = Status;
        this.TransactionStartDate = TransactionStartDate;
        this.TransactionVolumeMwh = TransactionVolumeMwh;
        this.VolumeMwh = VolumeMwh;
        this.EnergySource = EnergySource;
        this.Text = Text;
        this.Version = Version;
        this.CreatedAt = CreatedAt;
        this.UpdatedAt = UpdatedAt;
    }

    public required GooCertificateTransactionId GooCertificateTransactionId { get; init; }

    [TsOptional]
    public string? SalesforceTransactionId { get; init; }

    [TsOptional]
    public string? TransactionName { get; init; }

    [TsOptional]
    public string? BatchType { get; init; }

    [TsOptional]
    public string? CertificateTransactionId { get; init; }

    [TsOptional]
    public string? CountryOfProduction { get; init; }

    [TsOptional]
    public ContractId? ProducerContractId { get; init; }

    [TsOptional]
    public string? ProducerCompany { get; init; }

    [TsOptional]
    public Price? ProducerGooPriceEurMwh { get; init; }

    [TsOptional]
    public DateOnly? ProductionDate { get; init; }

    [TsOptional]
    public ContractId? CustomerContractId { get; init; }

    [TsOptional]
    public string? CustomerCompany { get; init; }

    [TsOptional]
    public string? Register { get; init; }

    [TsOptional]
    public string? Status { get; init; }

    [TsOptional]
    public DateOnly? TransactionStartDate { get; init; }

    [TsOptional]
    public Quantity? TransactionVolumeMwh { get; init; }

    [TsOptional]
    public Quantity? VolumeMwh { get; init; }

    [TsOptional]
    public string? EnergySource { get; init; }

    [TsOptional]
    public string? Text { get; init; }
    public required long Version { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
