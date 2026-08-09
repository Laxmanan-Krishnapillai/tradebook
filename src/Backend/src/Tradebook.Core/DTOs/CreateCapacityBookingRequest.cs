using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreateCapacityBookingRequest
{
    public CreateCapacityBookingRequest() { }

    [SetsRequiredMembers]
    public CreateCapacityBookingRequest(
        Guid ContractId,
        DateOnly SupplyMonth,
        string? ContractInstanceId,
        Guid? CounterpartyId,
        string? BalancingGroup,
        string? PriceMechanism,
        string? StartArea,
        string? EndArea,
        string? ShipFix,
        string? BorderPoint,
        DateOnly? StartDay,
        DateOnly? EndDay,
        decimal? CapacityMw,
        decimal? CapacityPriceEurMwh,
        decimal? CapacityCostEur,
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

    public required Guid ContractId { get; init; }
    public required DateOnly SupplyMonth { get; init; }

    [TsOptional]
    public string? ContractInstanceId { get; init; }

    [TsOptional]
    public Guid? CounterpartyId { get; init; }

    [TsOptional]
    public string? BalancingGroup { get; init; }

    [TsOptional]
    public string? PriceMechanism { get; init; }

    [TsOptional]
    public string? StartArea { get; init; }

    [TsOptional]
    public string? EndArea { get; init; }

    [TsOptional]
    public string? ShipFix { get; init; }

    [TsOptional]
    public string? BorderPoint { get; init; }

    [TsOptional]
    public DateOnly? StartDay { get; init; }

    [TsOptional]
    public DateOnly? EndDay { get; init; }

    [TsOptional]
    public decimal? CapacityMw { get; init; }

    [TsOptional]
    public decimal? CapacityPriceEurMwh { get; init; }

    [TsOptional]
    public decimal? CapacityCostEur { get; init; }

    [TsOptional]
    public string? Comments { get; init; }
}
