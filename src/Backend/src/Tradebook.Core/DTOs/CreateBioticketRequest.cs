using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreateBioticketRequest
{
    public CreateBioticketRequest() { }

    [SetsRequiredMembers]
    public CreateBioticketRequest(
        Guid ContractId,
        string BookType,
        DateOnly ContractMonth,
        string? ContractInstanceId,
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
        string? Status,
        string? Comment
    )
    {
        this.ContractId = ContractId;
        this.BookType = BookType;
        this.ContractMonth = ContractMonth;
        this.ContractInstanceId = ContractInstanceId;
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
    }

    public required Guid ContractId { get; init; }
    public required string BookType { get; init; }
    public required DateOnly ContractMonth { get; init; }

    [TsOptional]
    public string? ContractInstanceId { get; init; }

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

    [TsOptional]
    public string? Status { get; init; }

    [TsOptional]
    public string? Comment { get; init; }
}
