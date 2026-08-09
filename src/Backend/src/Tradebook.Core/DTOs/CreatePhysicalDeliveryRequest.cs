using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;

namespace Tradebook.Core.DTOs;

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

    public string? ContractInstanceId { get; init; }

    public required string BookType { get; init; }

    public required DateOnly SupplyMonth { get; init; }

    public Quantity? CapacityMw { get; init; }

    public Quantity? VolumeNominatedMwh { get; init; }

    public Quantity? VolumeRealisedMwh { get; init; }

    public string? PriceMechanism { get; init; }

    public DateOnly? StartDay { get; init; }

    public DateOnly? EndDay { get; init; }
}
