using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
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

    [TsOptional]
    public string? ContractInstanceId { get; init; }

    [TsOptional]
    public CounterpartyId? CounterpartyId { get; init; }

    [TsOptional]
    public string? BalancingGroup { get; init; }

    [TsOptional]
    public string? TradingArea { get; init; }

    [TsOptional]
    public Quantity? CapacityMw { get; init; }

    [TsOptional]
    public Quantity? BookedCapacityMw { get; init; }

    [TsOptional]
    public Quantity? VolumeMwh { get; init; }

    [TsOptional]
    public Quantity? BalancingEffectMwh { get; init; }

    [TsOptional]
    public DateOnly? StartDay { get; init; }

    [TsOptional]
    public DateOnly? EndDay { get; init; }

    [TsOptional]
    public string? PriceMechanism { get; init; }

    [TsOptional]
    public Amount? TransportCostEurMwh { get; init; }

    [TsOptional]
    public Quantity? CapacityCostEurMwh { get; init; }

    [TsOptional]
    public string? Status { get; init; }

    [TsOptional]
    public string? Comments { get; init; }
}
