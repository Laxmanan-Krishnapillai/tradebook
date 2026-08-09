using Tradebook.Core.Domain.ValueObjects.Ids;

namespace Tradebook.Core.Domain.Entities;

public sealed class PhysicalDelivery
{
    public DeliveryId Id { get; init; }
    public ContractId ContractId { get; init; }
    public required string ContractInstanceId { get; init; }
    public required string BookType { get; init; }
    public DateOnly SupplyMonth { get; init; }
    public long Version { get; init; }
}
