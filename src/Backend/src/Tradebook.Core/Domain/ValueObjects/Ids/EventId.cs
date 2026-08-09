using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Ids;

[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct EventId
{
    private static Validation Validate(Guid value) => IdValidation.Validate(value, nameof(EventId));

    public static EventId New() => From(Guid.CreateVersion7());
}
