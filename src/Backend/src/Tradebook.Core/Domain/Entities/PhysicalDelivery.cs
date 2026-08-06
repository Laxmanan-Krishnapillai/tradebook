namespace Tradebook.Core.Domain.Entities;

public sealed class PhysicalDelivery
{
    public Guid Id { get; init; }
    public Guid ContractId { get; init; }
    public required string ContractInstanceId { get; init; }
    public required string BookType { get; init; }
    public DateOnly SupplyMonth { get; init; }
    public long Version { get; init; }
}
