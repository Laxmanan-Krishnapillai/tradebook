using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Ids;

[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct BioticketDeliveryId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(BioticketDeliveryId));

    public static BioticketDeliveryId New() => From(Guid.CreateVersion7());
}
