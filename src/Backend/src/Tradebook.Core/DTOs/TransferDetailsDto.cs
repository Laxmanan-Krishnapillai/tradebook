using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record TransferDetailsDto
{
    public TransferDetailsDto() { }

    [SetsRequiredMembers]
    public TransferDetailsDto(
        Guid TransferId,
        Guid ContractId,
        string ContractInstanceId,
        DateOnly SupplyMonth,
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

    public required Guid TransferId { get; init; }
    public required Guid ContractId { get; init; }
    public required string ContractInstanceId { get; init; }
    public required DateOnly SupplyMonth { get; init; }

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
    public required long Version { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
