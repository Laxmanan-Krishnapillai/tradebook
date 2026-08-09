using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;

namespace Tradebook.Core.DTOs;

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

    public string? SalesforceTransactionId { get; init; }

    public string? TransactionName { get; init; }

    public string? BatchType { get; init; }

    public string? CertificateTransactionId { get; init; }

    public string? CountryOfProduction { get; init; }

    public ContractId? ProducerContractId { get; init; }

    public string? ProducerCompany { get; init; }

    public Price? ProducerGooPriceEurMwh { get; init; }

    public DateOnly? ProductionDate { get; init; }

    public ContractId? CustomerContractId { get; init; }

    public string? CustomerCompany { get; init; }

    public string? Register { get; init; }

    public string? Status { get; init; }

    public DateOnly? TransactionStartDate { get; init; }

    public Quantity? TransactionVolumeMwh { get; init; }

    public Quantity? VolumeMwh { get; init; }

    public string? EnergySource { get; init; }

    public string? Text { get; init; }
    public required long Version { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
