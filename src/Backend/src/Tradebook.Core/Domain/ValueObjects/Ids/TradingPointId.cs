using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Ids;

[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct TradingPointId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(TradingPointId));

    public static TradingPointId New() => From(Guid.CreateVersion7());
}
