using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record UpdateTransferRequest
{
    public UpdateTransferRequest() { }

    [SetsRequiredMembers]
    public UpdateTransferRequest(
        Guid TransferId,
        string? TradingArea,
        decimal? CapacityMw,
        decimal? BookedCapacityMw,
        decimal? VolumeMwh,
        decimal? BalancingEffectMwh,
        string? PriceMechanism,
        decimal? TransportCostEurMwh,
        decimal? CapacityCostEurMwh,
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

    public required Guid TransferId { get; init; }

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
}
