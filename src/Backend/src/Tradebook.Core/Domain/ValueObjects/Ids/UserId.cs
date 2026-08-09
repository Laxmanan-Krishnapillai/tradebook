using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Ids;

[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct UserId
{
    private static Validation Validate(Guid value) => IdValidation.Validate(value, nameof(UserId));

    public static UserId New() => From(Guid.CreateVersion7());
}
