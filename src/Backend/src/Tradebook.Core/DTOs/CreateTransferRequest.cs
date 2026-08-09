using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;

namespace Tradebook.Core.DTOs;

public sealed record CreateTransferRequest
{
    public CreateTransferRequest() { }

    [SetsRequiredMembers]
    public CreateTransferRequest(
        ContractId ContractId,
        DateOnly SupplyMonth,
        string? ContractInstanceId,
        CounterpartyId? CounterpartyId,
        string? BalancingGroup,
        string? TradingArea,
        Quantity? CapacityMw,
        Quantity? BookedCapacityMw,
        Quantity? VolumeMwh,
        Quantity? BalancingEffectMwh,
        DateOnly? StartDay,
        DateOnly? EndDay,
        string? PriceMechanism,
        Amount? TransportCostEurMwh,
        Quantity? CapacityCostEurMwh,
        string? Status,
        string? Comments
    )
    {
        this.ContractId = ContractId;
        this.SupplyMonth = SupplyMonth;
        this.ContractInstanceId = ContractInstanceId;
        this.CounterpartyId = CounterpartyId;
        this.BalancingGroup = BalancingGroup;
        this.TradingArea = TradingArea;
        this.CapacityMw = CapacityMw;
        this.BookedCapacityMw = BookedCapacityMw;
        this.VolumeMwh = VolumeMwh;
        this.BalancingEffectMwh = BalancingEffectMwh;
        this.StartDay = StartDay;
        this.EndDay = EndDay;
        this.PriceMechanism = PriceMechanism;
        this.TransportCostEurMwh = TransportCostEurMwh;
        this.CapacityCostEurMwh = CapacityCostEurMwh;
        this.Status = Status;
        this.Comments = Comments;
    }

    public required ContractId ContractId { get; init; }
    public required DateOnly SupplyMonth { get; init; }

    public string? ContractInstanceId { get; init; }

    public CounterpartyId? CounterpartyId { get; init; }

    public string? BalancingGroup { get; init; }

    public string? TradingArea { get; init; }

    public Quantity? CapacityMw { get; init; }

    public Quantity? BookedCapacityMw { get; init; }

    public Quantity? VolumeMwh { get; init; }

    public Quantity? BalancingEffectMwh { get; init; }

    public DateOnly? StartDay { get; init; }

    public DateOnly? EndDay { get; init; }

    public string? PriceMechanism { get; init; }

    public Amount? TransportCostEurMwh { get; init; }

    public Quantity? CapacityCostEurMwh { get; init; }

    public string? Status { get; init; }

    public string? Comments { get; init; }
}
