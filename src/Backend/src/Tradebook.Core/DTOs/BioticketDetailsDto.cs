using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record BioticketDetailsDto
{
    public BioticketDetailsDto() { }

    [SetsRequiredMembers]
    public BioticketDetailsDto(
        BioticketDeliveryId BioticketId,
        ContractId ContractId,
        string ContractInstanceId,
        string BookType,
        DateOnly ContractMonth,
        DateOnly? StartDay,
        DateOnly? EndDay,
        Quantity? VolumeNominatedTon,
        Quantity? VolumeRealisedTon,
        Quantity? VolumeTon,
        Amount? CostEurTon,
        Amount? RevenueEur,
        Amount? VatPct,
        Amount? VatEur,
        Amount? InvoiceAmountEur,
        string Status,
        string? Comment,
        long Version,
        DateTime CreatedAt,
        DateTime UpdatedAt
    )
    {
        this.BioticketId = BioticketId;
        this.ContractId = ContractId;
        this.ContractInstanceId = ContractInstanceId;
        this.BookType = BookType;
        this.ContractMonth = ContractMonth;
        this.StartDay = StartDay;
        this.EndDay = EndDay;
        this.VolumeNominatedTon = VolumeNominatedTon;
        this.VolumeRealisedTon = VolumeRealisedTon;
        this.VolumeTon = VolumeTon;
        this.CostEurTon = CostEurTon;
        this.RevenueEur = RevenueEur;
        this.VatPct = VatPct;
        this.VatEur = VatEur;
        this.InvoiceAmountEur = InvoiceAmountEur;
        this.Status = Status;
        this.Comment = Comment;
        this.Version = Version;
        this.CreatedAt = CreatedAt;
        this.UpdatedAt = UpdatedAt;
    }

    public required BioticketDeliveryId BioticketId { get; init; }
    public required ContractId ContractId { get; init; }
    public required string ContractInstanceId { get; init; }
    public required string BookType { get; init; }
    public required DateOnly ContractMonth { get; init; }

    [TsOptional]
    public DateOnly? StartDay { get; init; }

    [TsOptional]
    public DateOnly? EndDay { get; init; }

    [TsOptional]
    public Quantity? VolumeNominatedTon { get; init; }

    [TsOptional]
    public Quantity? VolumeRealisedTon { get; init; }

    [TsOptional]
    public Quantity? VolumeTon { get; init; }

    [TsOptional]
    public Amount? CostEurTon { get; init; }

    [TsOptional]
    public Amount? RevenueEur { get; init; }

    [TsOptional]
    public Amount? VatPct { get; init; }

    [TsOptional]
    public Amount? VatEur { get; init; }

    [TsOptional]
    public Amount? InvoiceAmountEur { get; init; }
    public required string Status { get; init; }

    [TsOptional]
    public string? Comment { get; init; }
    public required long Version { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
