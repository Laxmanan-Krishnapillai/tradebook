using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Ids;

[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct HedgeId
{
    private static Validation Validate(Guid value) => IdValidation.Validate(value, nameof(HedgeId));

    public static HedgeId New() => From(Guid.CreateVersion7());
}
