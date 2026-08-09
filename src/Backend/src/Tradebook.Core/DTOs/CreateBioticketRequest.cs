using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;

namespace Tradebook.Core.DTOs;

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

    public string? ContractInstanceId { get; init; }

    public DateOnly? StartDay { get; init; }

    public DateOnly? EndDay { get; init; }

    public Quantity? VolumeNominatedTon { get; init; }

    public Quantity? VolumeRealisedTon { get; init; }

    public Quantity? VolumeTon { get; init; }

    public Amount? CostEurTon { get; init; }

    public Amount? RevenueEur { get; init; }

    public Amount? VatPct { get; init; }

    public Amount? VatEur { get; init; }

    public Amount? InvoiceAmountEur { get; init; }

    public string? Status { get; init; }

    public string? Comment { get; init; }
}
