using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreatePhysicalDeliveryResponse
{
    public CreatePhysicalDeliveryResponse() { }

    [SetsRequiredMembers]
    public CreatePhysicalDeliveryResponse(
        DeliveryId DeliveryId,
        string ContractInstanceId,
        Amount? InvoiceAmountEur,
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

    public required DeliveryId DeliveryId { get; init; }

    public required string ContractInstanceId { get; init; }

    [TsOptional]
    public Amount? InvoiceAmountEur { get; init; }

    public required string Status { get; init; }

    public required long Version { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
