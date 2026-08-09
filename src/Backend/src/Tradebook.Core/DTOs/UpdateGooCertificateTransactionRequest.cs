using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;

namespace Tradebook.Core.DTOs;

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

    public string? BatchType { get; init; }

    public ContractId? ProducerContractId { get; init; }

    public ContractId? CustomerContractId { get; init; }

    public string? Register { get; init; }

    public string? Status { get; init; }

    public DateOnly? TransactionStartDate { get; init; }

    public Quantity? TransactionVolumeMwh { get; init; }

    public Quantity? VolumeMwh { get; init; }

    public string? Text { get; init; }
    public required long Version { get; init; }
}
