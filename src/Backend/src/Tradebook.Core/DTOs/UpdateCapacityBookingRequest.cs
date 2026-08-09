using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
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

    [TsOptional]
    public string? BalancingGroup { get; init; }

    [TsOptional]
    public string? PriceMechanism { get; init; }

    [TsOptional]
    public string? StartArea { get; init; }

    [TsOptional]
    public string? EndArea { get; init; }

    [TsOptional]
    public DateOnly? StartDay { get; init; }

    [TsOptional]
    public DateOnly? EndDay { get; init; }

    [TsOptional]
    public Quantity? CapacityMw { get; init; }

    [TsOptional]
    public Quantity? CapacityPriceEurMwh { get; init; }

    [TsOptional]
    public Quantity? CapacityCostEur { get; init; }

    [TsOptional]
    public string? Comments { get; init; }
    public required long Version { get; init; }
}
