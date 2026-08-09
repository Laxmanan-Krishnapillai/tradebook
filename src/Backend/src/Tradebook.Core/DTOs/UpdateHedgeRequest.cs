using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record UpdateHedgeRequest
{
    public UpdateHedgeRequest() { }

    [SetsRequiredMembers]
    public UpdateHedgeRequest(
        Guid HedgeId,
        decimal? HedgeAmountMwh,
        decimal? HedgePriceEurMwh,
        long Version
    )
    {
        this.HedgeId = HedgeId;
        this.HedgeAmountMwh = HedgeAmountMwh;
        this.HedgePriceEurMwh = HedgePriceEurMwh;
        this.Version = Version;
    }

    public required Guid HedgeId { get; init; }

    [TsOptional]
    public decimal? HedgeAmountMwh { get; init; }

    [TsOptional]
    public decimal? HedgePriceEurMwh { get; init; }

    public required long Version { get; init; }
}
