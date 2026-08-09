using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;

namespace Tradebook.Core.DTOs;

public sealed record HedgeDetailsDto
{
    public HedgeDetailsDto() { }

    [SetsRequiredMembers]
    public HedgeDetailsDto(
        HedgeId HedgeId,
        ContractId ContractId,
        DateOnly Month,
        Quantity? HedgeAmountMwh,
        Price? HedgePriceEurMwh,
        long Version,
        DateTime CreatedAt,
        DateTime UpdatedAt
    )
    {
        this.HedgeId = HedgeId;
        this.ContractId = ContractId;
        this.Month = Month;
        this.HedgeAmountMwh = HedgeAmountMwh;
        this.HedgePriceEurMwh = HedgePriceEurMwh;
        this.Version = Version;
        this.CreatedAt = CreatedAt;
        this.UpdatedAt = UpdatedAt;
    }

    public required HedgeId HedgeId { get; init; }

    public required ContractId ContractId { get; init; }

    public required DateOnly Month { get; init; }

    public Quantity? HedgeAmountMwh { get; init; }

    public Price? HedgePriceEurMwh { get; init; }

    public required long Version { get; init; }

    public required DateTime CreatedAt { get; init; }

    public required DateTime UpdatedAt { get; init; }
}
