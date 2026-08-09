using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record PhysicalDeliveryDetailsDto
{
    public PhysicalDeliveryDetailsDto() { }

    [SetsRequiredMembers]
    public PhysicalDeliveryDetailsDto(
        Guid DeliveryId,
        Guid ContractId,
        string ContractInstanceId,
        string BookType,
        DateOnly SupplyMonth,
        decimal? CapacityMw,
        decimal? VolumeNominatedMwh,
        decimal? VolumeRealisedMwh,
        decimal? VolumeMwh,
        string? PriceMechanism,
        decimal? RevenueEur,
        decimal? SubtotalEur,
        decimal? VatEur,
        decimal? InvoiceAmountEur,
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

    public required Guid DeliveryId { get; init; }

    public required Guid ContractId { get; init; }

    public required string ContractInstanceId { get; init; }

    public required string BookType { get; init; }

    public required DateOnly SupplyMonth { get; init; }

    [TsOptional]
    public decimal? CapacityMw { get; init; }

    [TsOptional]
    public decimal? VolumeNominatedMwh { get; init; }

    [TsOptional]
    public decimal? VolumeRealisedMwh { get; init; }

    [TsOptional]
    public decimal? VolumeMwh { get; init; }

    [TsOptional]
    public string? PriceMechanism { get; init; }

    [TsOptional]
    public decimal? RevenueEur { get; init; }

    [TsOptional]
    public decimal? SubtotalEur { get; init; }

    [TsOptional]
    public decimal? VatEur { get; init; }

    [TsOptional]
    public decimal? InvoiceAmountEur { get; init; }

    public required string Status { get; init; }

    public required long Version { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}
