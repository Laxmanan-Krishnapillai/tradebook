using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;

namespace Tradebook.Core.DTOs;

public sealed record UpdateBioticketRequest
{
    public UpdateBioticketRequest() { }

    [SetsRequiredMembers]
    public UpdateBioticketRequest(
        BioticketDeliveryId BioticketId,
        Quantity? VolumeRealisedTon,
        Quantity? VolumeTon,
        Amount? CostEurTon,
        Amount? RevenueEur,
        Amount? VatPct,
        Amount? VatEur,
        Amount? InvoiceAmountEur,
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

    public required BioticketDeliveryId BioticketId { get; init; }

    public Quantity? VolumeRealisedTon { get; init; }

    public Quantity? VolumeTon { get; init; }

    public Amount? CostEurTon { get; init; }

    public Amount? RevenueEur { get; init; }

    public Amount? VatPct { get; init; }

    public Amount? VatEur { get; init; }

    public Amount? InvoiceAmountEur { get; init; }

    public string? Status { get; init; }

    public string? Comment { get; init; }
    public required long Version { get; init; }
}
