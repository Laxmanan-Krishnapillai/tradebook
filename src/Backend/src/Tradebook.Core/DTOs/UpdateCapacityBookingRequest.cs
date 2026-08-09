using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;

namespace Tradebook.Core.DTOs;

public sealed record UpdateCapacityBookingRequest
{
    public UpdateCapacityBookingRequest() { }

    [SetsRequiredMembers]
    public UpdateCapacityBookingRequest(
        CapacityBookingId CapacityBookingId,
        string? BalancingGroup,
        string? PriceMechanism,
        string? StartArea,
        string? EndArea,
        DateOnly? StartDay,
        DateOnly? EndDay,
        Quantity? CapacityMw,
        Quantity? CapacityPriceEurMwh,
        Quantity? CapacityCostEur,
        string? Comments,
        long Version
    )
    {
        this.CapacityBookingId = CapacityBookingId;
        this.BalancingGroup = BalancingGroup;
        this.PriceMechanism = PriceMechanism;
        this.StartArea = StartArea;
        this.EndArea = EndArea;
        this.StartDay = StartDay;
        this.EndDay = EndDay;
        this.CapacityMw = CapacityMw;
        this.CapacityPriceEurMwh = CapacityPriceEurMwh;
        this.CapacityCostEur = CapacityCostEur;
        this.Comments = Comments;
        this.Version = Version;
    }

    public required CapacityBookingId CapacityBookingId { get; init; }

    public string? BalancingGroup { get; init; }

    public string? PriceMechanism { get; init; }

    public string? StartArea { get; init; }

    public string? EndArea { get; init; }

    public DateOnly? StartDay { get; init; }

    public DateOnly? EndDay { get; init; }

    public Quantity? CapacityMw { get; init; }

    public Quantity? CapacityPriceEurMwh { get; init; }

    public Quantity? CapacityCostEur { get; init; }

    public string? Comments { get; init; }
    public required long Version { get; init; }
}
