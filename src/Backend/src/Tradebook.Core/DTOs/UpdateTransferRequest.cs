using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;

namespace Tradebook.Core.DTOs;

public sealed record UpdateTransferRequest
{
    public UpdateTransferRequest() { }

    [SetsRequiredMembers]
    public UpdateTransferRequest(
        TransferId TransferId,
        string? TradingArea,
        Quantity? CapacityMw,
        Quantity? BookedCapacityMw,
        Quantity? VolumeMwh,
        Quantity? BalancingEffectMwh,
        string? PriceMechanism,
        Amount? TransportCostEurMwh,
        Quantity? CapacityCostEurMwh,
        string? Status,
        string? Comments,
        long Version
    )
    {
        this.TransferId = TransferId;
        this.TradingArea = TradingArea;
        this.CapacityMw = CapacityMw;
        this.BookedCapacityMw = BookedCapacityMw;
        this.VolumeMwh = VolumeMwh;
        this.BalancingEffectMwh = BalancingEffectMwh;
        this.PriceMechanism = PriceMechanism;
        this.TransportCostEurMwh = TransportCostEurMwh;
        this.CapacityCostEurMwh = CapacityCostEurMwh;
        this.Status = Status;
        this.Comments = Comments;
        this.Version = Version;
    }

    public required TransferId TransferId { get; init; }

    public string? TradingArea { get; init; }

    public Quantity? CapacityMw { get; init; }

    public Quantity? BookedCapacityMw { get; init; }

    public Quantity? VolumeMwh { get; init; }

    public Quantity? BalancingEffectMwh { get; init; }

    public string? PriceMechanism { get; init; }

    public Amount? TransportCostEurMwh { get; init; }

    public Quantity? CapacityCostEurMwh { get; init; }

    public string? Status { get; init; }

    public string? Comments { get; init; }
    public required long Version { get; init; }
}
