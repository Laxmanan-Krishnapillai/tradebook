using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Ids;

[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct CompanyId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(CompanyId));

    public static CompanyId New() => From(Guid.CreateVersion7());
}
