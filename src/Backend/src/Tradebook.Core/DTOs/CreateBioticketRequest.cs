using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreateBioticketRequest
{
    public CreateBioticketRequest() { }

    [SetsRequiredMembers]
    public CreateBioticketRequest(
        ContractId ContractId,
        string BookType,
        DateOnly ContractMonth,
        string? ContractInstanceId,
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

    public required ContractId ContractId { get; init; }
    public required string BookType { get; init; }
    public required DateOnly ContractMonth { get; init; }

    [TsOptional]
    public string? ContractInstanceId { get; init; }

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

    [TsOptional]
    public string? Status { get; init; }

    [TsOptional]
    public string? Comment { get; init; }
}
