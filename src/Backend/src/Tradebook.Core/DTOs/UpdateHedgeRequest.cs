using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record UpdateHedgeRequest
{
    public UpdateHedgeRequest() { }

    [SetsRequiredMembers]
    public UpdateHedgeRequest(
        HedgeId HedgeId,
        Quantity? HedgeAmountMwh,
        Price? HedgePriceEurMwh,
        long Version
    )
    {
        this.HedgeId = HedgeId;
        this.HedgeAmountMwh = HedgeAmountMwh;
        this.HedgePriceEurMwh = HedgePriceEurMwh;
        this.Version = Version;
    }

    public required HedgeId HedgeId { get; init; }

    [TsOptional]
    public Quantity? HedgeAmountMwh { get; init; }

    [TsOptional]
    public Price? HedgePriceEurMwh { get; init; }

    public required long Version { get; init; }
}
