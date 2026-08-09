using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreateGooCertificateTransactionRequest
{
    public CreateGooCertificateTransactionRequest() { }

    public CreateGooCertificateTransactionRequest(
        string? SalesforceTransactionId,
        string? TransactionName,
        string? BatchType,
        string? CertificateTransactionId,
        string? CountryOfProduction,
        Guid? ProducerContractId,
        string? ProducerCompany,
        decimal? ProducerGooPriceEurMwh,
        DateOnly? ProductionDate,
        Guid? CustomerContractId,
        string? CustomerCompany,
        string? Register,
        string? Status,
        DateOnly? TransactionStartDate,
        decimal? TransactionVolumeMwh,
        decimal? VolumeMwh,
        string? EnergySource,
        string? Text
    )
    {
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
    }

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
    public Guid? ProducerContractId { get; init; }

    [TsOptional]
    public string? ProducerCompany { get; init; }

    [TsOptional]
    public decimal? ProducerGooPriceEurMwh { get; init; }

    [TsOptional]
    public DateOnly? ProductionDate { get; init; }

    [TsOptional]
    public Guid? CustomerContractId { get; init; }

    [TsOptional]
    public string? CustomerCompany { get; init; }

    [TsOptional]
    public string? Register { get; init; }

    [TsOptional]
    public string? Status { get; init; }

    [TsOptional]
    public DateOnly? TransactionStartDate { get; init; }

    [TsOptional]
    public decimal? TransactionVolumeMwh { get; init; }

    [TsOptional]
    public decimal? VolumeMwh { get; init; }

    [TsOptional]
    public string? EnergySource { get; init; }

    [TsOptional]
    public string? Text { get; init; }
}
