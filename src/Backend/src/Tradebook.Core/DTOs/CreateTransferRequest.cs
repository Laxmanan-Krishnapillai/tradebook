using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreateTransferRequest
{
    public CreateTransferRequest() { }

    [SetsRequiredMembers]
    public CreateTransferRequest(
        Guid ContractId,
        DateOnly SupplyMonth,
        string? ContractInstanceId,
        Guid? CounterpartyId,
        string? BalancingGroup,
        string? TradingArea,
        decimal? CapacityMw,
        decimal? BookedCapacityMw,
        decimal? VolumeMwh,
        decimal? BalancingEffectMwh,
        DateOnly? StartDay,
        DateOnly? EndDay,
        string? PriceMechanism,
        decimal? TransportCostEurMwh,
        decimal? CapacityCostEurMwh,
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

    public required Guid ContractId { get; init; }
    public required DateOnly SupplyMonth { get; init; }

    [TsOptional]
    public string? ContractInstanceId { get; init; }

    [TsOptional]
    public Guid? CounterpartyId { get; init; }

    [TsOptional]
    public string? BalancingGroup { get; init; }

    [TsOptional]
    public string? TradingArea { get; init; }

    [TsOptional]
    public decimal? CapacityMw { get; init; }

    [TsOptional]
    public decimal? BookedCapacityMw { get; init; }

    [TsOptional]
    public decimal? VolumeMwh { get; init; }

    [TsOptional]
    public decimal? BalancingEffectMwh { get; init; }

    [TsOptional]
    public DateOnly? StartDay { get; init; }

    [TsOptional]
    public DateOnly? EndDay { get; init; }

    [TsOptional]
    public string? PriceMechanism { get; init; }

    [TsOptional]
    public decimal? TransportCostEurMwh { get; init; }

    [TsOptional]
    public decimal? CapacityCostEurMwh { get; init; }

    [TsOptional]
    public string? Status { get; init; }

    [TsOptional]
    public string? Comments { get; init; }
}
