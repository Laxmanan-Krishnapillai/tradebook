using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Ids;

[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct DeliveryId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(DeliveryId));

    public static DeliveryId New() => From(Guid.CreateVersion7());
}
