using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreatePhysicalDeliveryRequest
{
    public CreatePhysicalDeliveryRequest() { }

    [SetsRequiredMembers]
    public CreatePhysicalDeliveryRequest(
        Guid ContractId,
        string? ContractInstanceId,
        string BookType,
        DateOnly SupplyMonth,
        decimal? CapacityMw,
        decimal? VolumeNominatedMwh,
        decimal? VolumeRealisedMwh,
        string? PriceMechanism,
        DateOnly? StartDay,
        DateOnly? EndDay
    )
    {
        this.ContractId = ContractId;
        this.ContractInstanceId = ContractInstanceId;
        this.BookType = BookType;
        this.SupplyMonth = SupplyMonth;
        this.CapacityMw = CapacityMw;
        this.VolumeNominatedMwh = VolumeNominatedMwh;
        this.VolumeRealisedMwh = VolumeRealisedMwh;
        this.PriceMechanism = PriceMechanism;
        this.StartDay = StartDay;
        this.EndDay = EndDay;
    }

    public required Guid ContractId { get; init; }

    [TsOptional]
    public string? ContractInstanceId { get; init; }

    public required string BookType { get; init; }

    public required DateOnly SupplyMonth { get; init; }

    [TsOptional]
    public decimal? CapacityMw { get; init; }

    [TsOptional]
    public decimal? VolumeNominatedMwh { get; init; }

    [TsOptional]
    public decimal? VolumeRealisedMwh { get; init; }

    [TsOptional]
    public string? PriceMechanism { get; init; }

    [TsOptional]
    public DateOnly? StartDay { get; init; }

    [TsOptional]
    public DateOnly? EndDay { get; init; }
}
