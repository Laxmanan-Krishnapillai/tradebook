using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;

namespace Tradebook.Core.DTOs;

public sealed record CreateHedgeRequest
{
    public CreateHedgeRequest() { }

    [SetsRequiredMembers]
    public CreateHedgeRequest(
        ContractId ContractId,
        DateOnly Month,
        Quantity? HedgeAmountMwh,
        Price? HedgePriceEurMwh
    )
    {
        this.ContractId = ContractId;
        this.Month = Month;
        this.HedgeAmountMwh = HedgeAmountMwh;
        this.HedgePriceEurMwh = HedgePriceEurMwh;
    }

    public required ContractId ContractId { get; init; }

    public required DateOnly Month { get; init; }

    public Quantity? HedgeAmountMwh { get; init; }

    public Price? HedgePriceEurMwh { get; init; }
}
