using Tradebook.Core.Domain.ValueObjects.Ids;

namespace Tradebook.Core.Domain.Entities;

public sealed class Counterparty
{
    public CounterpartyId Id { get; init; }
    public required string Name { get; init; }
    public required string Shorthand { get; init; }
    public long Version { get; init; }
}
