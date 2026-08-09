using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;

namespace Tradebook.Core.DTOs;

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

    public Quantity? HedgeAmountMwh { get; init; }

    public Price? HedgePriceEurMwh { get; init; }

    public required long Version { get; init; }
}
