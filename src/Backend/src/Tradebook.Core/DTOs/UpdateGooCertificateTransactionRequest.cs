using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record UpdateGooCertificateTransactionRequest
{
    public UpdateGooCertificateTransactionRequest() { }

    [SetsRequiredMembers]
    public UpdateGooCertificateTransactionRequest(
        GooCertificateTransactionId GooCertificateTransactionId,
        string? BatchType,
        ContractId? ProducerContractId,
        ContractId? CustomerContractId,
        string? Register,
        string? Status,
        DateOnly? TransactionStartDate,
        Quantity? TransactionVolumeMwh,
        Quantity? VolumeMwh,
        string? Text,
        long Version
    )
    {
        this.GooCertificateTransactionId = GooCertificateTransactionId;
        this.BatchType = BatchType;
        this.ProducerContractId = ProducerContractId;
        this.CustomerContractId = CustomerContractId;
        this.Register = Register;
        this.Status = Status;
        this.TransactionStartDate = TransactionStartDate;
        this.TransactionVolumeMwh = TransactionVolumeMwh;
        this.VolumeMwh = VolumeMwh;
        this.Text = Text;
        this.Version = Version;
    }

    public required GooCertificateTransactionId GooCertificateTransactionId { get; init; }

    [TsOptional]
    public string? BatchType { get; init; }

    [TsOptional]
    public ContractId? ProducerContractId { get; init; }

    [TsOptional]
    public ContractId? CustomerContractId { get; init; }

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
    public string? Text { get; init; }
    public required long Version { get; init; }
}
