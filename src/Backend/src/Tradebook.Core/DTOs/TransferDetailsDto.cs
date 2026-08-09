using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;

namespace Tradebook.Core.DTOs;

public sealed record TransferDetailsDto
{
    public TransferDetailsDto() { }

    [SetsRequiredMembers]
    public TransferDetailsDto(
        TransferId TransferId,
        ContractId ContractId,
        string ContractInstanceId,
        DateOnly SupplyMonth,
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
        string? Comments,
        long Version,
        DateTime CreatedAt,
        DateTime UpdatedAt
    )
    {
        this.TransferId = TransferId;
        this.ContractId = ContractId;
        this.ContractInstanceId = ContractInstanceId;
        this.SupplyMonth = SupplyMonth;
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
        this.Version = Version;
        this.CreatedAt = CreatedAt;
        this.UpdatedAt = UpdatedAt;
    }

    public required TransferId TransferId { get; init; }
    public required ContractId ContractId { get; init; }
    public required string ContractInstanceId { get; init; }
    public required DateOnly SupplyMonth { get; init; }

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
    public required long Version { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
