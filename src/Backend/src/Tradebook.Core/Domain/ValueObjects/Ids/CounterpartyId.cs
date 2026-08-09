using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Ids;

[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct CounterpartyId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(CounterpartyId));

    public static CounterpartyId New() => From(Guid.CreateVersion7());
}
