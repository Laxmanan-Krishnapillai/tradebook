using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Ids;

[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct TransferId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(TransferId));

    public static TransferId New() => From(Guid.CreateVersion7());
}
