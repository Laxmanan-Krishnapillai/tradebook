using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Ids;

[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct CapacityBookingId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(CapacityBookingId));

    public static CapacityBookingId New() => From(Guid.CreateVersion7());
}
