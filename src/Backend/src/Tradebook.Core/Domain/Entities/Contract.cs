using Tradebook.Core.Domain.ValueObjects.Ids;

namespace Tradebook.Core.Domain.Entities;

public sealed class Contract
{
    public ContractId Id { get; init; }
    public required string ContractName { get; init; }
    public long Version { get; init; }
}
