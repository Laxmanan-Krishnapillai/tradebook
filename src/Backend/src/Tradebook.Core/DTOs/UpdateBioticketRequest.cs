using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
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
    public required long Version { get; init; }
}
