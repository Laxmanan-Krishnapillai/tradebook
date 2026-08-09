using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Ids;

[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct CapacityPriceIndexId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(CapacityPriceIndexId));

    public static CapacityPriceIndexId New() => From(Guid.CreateVersion7());
}
