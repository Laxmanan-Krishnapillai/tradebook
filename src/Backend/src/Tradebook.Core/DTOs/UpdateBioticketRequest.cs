using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record UpdateBioticketRequest
{
    public UpdateBioticketRequest() { }

    [SetsRequiredMembers]
    public UpdateBioticketRequest(
        Guid BioticketId,
        decimal? VolumeRealisedTon,
        decimal? VolumeTon,
        decimal? CostEurTon,
        decimal? RevenueEur,
        decimal? VatPct,
        decimal? VatEur,
        decimal? InvoiceAmountEur,
        string? Status,
        string? Comment,
        long Version
    )
    {
        this.BioticketId = BioticketId;
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
    }

    public required Guid BioticketId { get; init; }

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

    [TsOptional]
    public string? Status { get; init; }

    [TsOptional]
    public string? Comment { get; init; }
    public required long Version { get; init; }
}
