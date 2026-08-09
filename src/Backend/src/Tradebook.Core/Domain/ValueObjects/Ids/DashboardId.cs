using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Ids;

[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct DashboardId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(DashboardId));

    public static DashboardId New() => From(Guid.CreateVersion7());
}
