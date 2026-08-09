using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Ids;

[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct MarketPriceId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(MarketPriceId));

    public static MarketPriceId New() => From(Guid.CreateVersion7());
}
