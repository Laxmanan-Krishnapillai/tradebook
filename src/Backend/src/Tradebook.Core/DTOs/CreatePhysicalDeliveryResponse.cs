using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreatePhysicalDeliveryResponse
{
    public CreatePhysicalDeliveryResponse() { }

    [SetsRequiredMembers]
    public CreatePhysicalDeliveryResponse(
        Guid DeliveryId,
        string ContractInstanceId,
        decimal? InvoiceAmountEur,
        string Status,
        long Version,
        DateTimeOffset CreatedAt
    )
    {
        this.DeliveryId = DeliveryId;
        this.ContractInstanceId = ContractInstanceId;
        this.InvoiceAmountEur = InvoiceAmountEur;
        this.Status = Status;
        this.Version = Version;
        this.CreatedAt = CreatedAt;
    }

    public required Guid DeliveryId { get; init; }

    public required string ContractInstanceId { get; init; }

    [TsOptional]
    public decimal? InvoiceAmountEur { get; init; }

    public required string Status { get; init; }

    public required long Version { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
