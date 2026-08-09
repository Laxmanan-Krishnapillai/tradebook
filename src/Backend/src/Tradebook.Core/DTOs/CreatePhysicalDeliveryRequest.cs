using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreatePhysicalDeliveryRequest
{
    public CreatePhysicalDeliveryRequest() { }

    [SetsRequiredMembers]
    public CreatePhysicalDeliveryRequest(
        ContractId ContractId,
        string? ContractInstanceId,
        string BookType,
        DateOnly SupplyMonth,
        Quantity? CapacityMw,
        Quantity? VolumeNominatedMwh,
        Quantity? VolumeRealisedMwh,
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

    public required ContractId ContractId { get; init; }

    [TsOptional]
    public string? ContractInstanceId { get; init; }

    public required string BookType { get; init; }

    public required DateOnly SupplyMonth { get; init; }

    [TsOptional]
    public Quantity? CapacityMw { get; init; }

    [TsOptional]
    public Quantity? VolumeNominatedMwh { get; init; }

    [TsOptional]
    public Quantity? VolumeRealisedMwh { get; init; }

    [TsOptional]
    public string? PriceMechanism { get; init; }

    [TsOptional]
    public DateOnly? StartDay { get; init; }

    [TsOptional]
    public DateOnly? EndDay { get; init; }
}
