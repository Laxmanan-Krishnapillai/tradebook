using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record PhysicalDeliveryDetailsDto
{
    public PhysicalDeliveryDetailsDto() { }

    [SetsRequiredMembers]
    public PhysicalDeliveryDetailsDto(
        DeliveryId DeliveryId,
        ContractId ContractId,
        string ContractInstanceId,
        string BookType,
        DateOnly SupplyMonth,
        Quantity? CapacityMw,
        Quantity? VolumeNominatedMwh,
        Quantity? VolumeRealisedMwh,
        Quantity? VolumeMwh,
        string? PriceMechanism,
        Amount? RevenueEur,
        Amount? SubtotalEur,
        Amount? VatEur,
        Amount? InvoiceAmountEur,
        string Status,
        long Version,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt
    )
    {
        this.DeliveryId = DeliveryId;
        this.ContractId = ContractId;
        this.ContractInstanceId = ContractInstanceId;
        this.BookType = BookType;
        this.SupplyMonth = SupplyMonth;
        this.CapacityMw = CapacityMw;
        this.VolumeNominatedMwh = VolumeNominatedMwh;
        this.VolumeRealisedMwh = VolumeRealisedMwh;
        this.VolumeMwh = VolumeMwh;
        this.PriceMechanism = PriceMechanism;
        this.RevenueEur = RevenueEur;
        this.SubtotalEur = SubtotalEur;
        this.VatEur = VatEur;
        this.InvoiceAmountEur = InvoiceAmountEur;
        this.Status = Status;
        this.Version = Version;
        this.CreatedAt = CreatedAt;
        this.UpdatedAt = UpdatedAt;
    }

    public required DeliveryId DeliveryId { get; init; }

    public required ContractId ContractId { get; init; }

    public required string ContractInstanceId { get; init; }

    public required string BookType { get; init; }

    public required DateOnly SupplyMonth { get; init; }

    [TsOptional]
    public Quantity? CapacityMw { get; init; }

    [TsOptional]
    public Quantity? VolumeNominatedMwh { get; init; }

    [TsOptional]
    public Quantity? VolumeRealisedMwh { get; init; }

    [TsOptional]
    public Quantity? VolumeMwh { get; init; }

    [TsOptional]
    public string? PriceMechanism { get; init; }

    [TsOptional]
    public Amount? RevenueEur { get; init; }

    [TsOptional]
    public Amount? SubtotalEur { get; init; }

    [TsOptional]
    public Amount? VatEur { get; init; }

    [TsOptional]
    public Amount? InvoiceAmountEur { get; init; }

    public required string Status { get; init; }

    public required long Version { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}
