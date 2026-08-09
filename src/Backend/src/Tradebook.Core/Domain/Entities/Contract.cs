namespace Tradebook.Core.Domain.Entities;

public sealed class Contract
{
    public Guid Id { get; init; }
    public required string ContractName { get; init; }
    public long Version { get; init; }
}
