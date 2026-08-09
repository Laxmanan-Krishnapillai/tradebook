using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record BioticketDetailsDto
{
    public BioticketDetailsDto() { }

    [SetsRequiredMembers]
    public BioticketDetailsDto(
        Guid BioticketId,
        Guid ContractId,
        string ContractInstanceId,
        string BookType,
        DateOnly ContractMonth,
        DateOnly? StartDay,
        DateOnly? EndDay,
        decimal? VolumeNominatedTon,
        decimal? VolumeRealisedTon,
        decimal? VolumeTon,
        decimal? CostEurTon,
        decimal? RevenueEur,
        decimal? VatPct,
        decimal? VatEur,
        decimal? InvoiceAmountEur,
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

    public required Guid BioticketId { get; init; }
    public required Guid ContractId { get; init; }
    public required string ContractInstanceId { get; init; }
    public required string BookType { get; init; }
    public required DateOnly ContractMonth { get; init; }

    [TsOptional]
    public DateOnly? StartDay { get; init; }

    [TsOptional]
    public DateOnly? EndDay { get; init; }

    [TsOptional]
    public decimal? VolumeNominatedTon { get; init; }

    [TsOptional]
    public decimal? VolumeRealisedTon { get; init; }

    [TsOptional]
    public decimal? VolumeTon { get; init; }

    [TsOptional]
    public decimal? CostEurTon { get; init; }

    [TsOptional]
    public decimal? RevenueEur { get; init; }

    [TsOptional]
    public decimal? VatPct { get; init; }

    [TsOptional]
    public decimal? VatEur { get; init; }

    [TsOptional]
    public decimal? InvoiceAmountEur { get; init; }
    public required string Status { get; init; }

    [TsOptional]
    public string? Comment { get; init; }
    public required long Version { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
