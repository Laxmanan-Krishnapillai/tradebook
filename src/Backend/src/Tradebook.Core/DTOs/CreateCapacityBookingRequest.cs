using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;

namespace Tradebook.Core.DTOs;

public sealed record CreateCapacityBookingRequest
{
    public CreateCapacityBookingRequest() { }

    [SetsRequiredMembers]
    public CreateCapacityBookingRequest(
        ContractId ContractId,
        DateOnly SupplyMonth,
        string? ContractInstanceId,
        CounterpartyId? CounterpartyId,
        string? BalancingGroup,
        string? PriceMechanism,
        string? StartArea,
        string? EndArea,
        string? ShipFix,
        string? BorderPoint,
        DateOnly? StartDay,
        DateOnly? EndDay,
        Quantity? CapacityMw,
        Quantity? CapacityPriceEurMwh,
        Quantity? CapacityCostEur,
        string? Comments
    )
    {
        this.ContractId = ContractId;
        this.SupplyMonth = SupplyMonth;
        this.ContractInstanceId = ContractInstanceId;
        this.CounterpartyId = CounterpartyId;
        this.BalancingGroup = BalancingGroup;
        this.PriceMechanism = PriceMechanism;
        this.StartArea = StartArea;
        this.EndArea = EndArea;
        this.ShipFix = ShipFix;
        this.BorderPoint = BorderPoint;
        this.StartDay = StartDay;
        this.EndDay = EndDay;
        this.CapacityMw = CapacityMw;
        this.CapacityPriceEurMwh = CapacityPriceEurMwh;
        this.CapacityCostEur = CapacityCostEur;
        this.Comments = Comments;
    }

    public required ContractId ContractId { get; init; }
    public required DateOnly SupplyMonth { get; init; }

    public string? ContractInstanceId { get; init; }

    public CounterpartyId? CounterpartyId { get; init; }

    public string? BalancingGroup { get; init; }

    public string? PriceMechanism { get; init; }

    public string? StartArea { get; init; }

    public string? EndArea { get; init; }

    public string? ShipFix { get; init; }

    public string? BorderPoint { get; init; }

    public DateOnly? StartDay { get; init; }

    public DateOnly? EndDay { get; init; }

    public Quantity? CapacityMw { get; init; }

    public Quantity? CapacityPriceEurMwh { get; init; }

    public Quantity? CapacityCostEur { get; init; }

    public string? Comments { get; init; }
}
