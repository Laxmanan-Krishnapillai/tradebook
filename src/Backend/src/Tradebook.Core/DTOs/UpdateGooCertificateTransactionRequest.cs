using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record UpdateGooCertificateTransactionRequest
{
    public UpdateGooCertificateTransactionRequest() { }

    [SetsRequiredMembers]
    public UpdateGooCertificateTransactionRequest(
        Guid GooCertificateTransactionId,
        string? BatchType,
        Guid? ProducerContractId,
        Guid? CustomerContractId,
        string? Register,
        string? Status,
        DateOnly? TransactionStartDate,
        decimal? TransactionVolumeMwh,
        decimal? VolumeMwh,
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

    public required Guid GooCertificateTransactionId { get; init; }

    [TsOptional]
    public string? BatchType { get; init; }

    [TsOptional]
    public Guid? ProducerContractId { get; init; }

    [TsOptional]
    public Guid? CustomerContractId { get; init; }

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
    public string? Text { get; init; }
    public required long Version { get; init; }
}
