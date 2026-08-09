using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Ids;

[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct TaxTariffId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(TaxTariffId));

    public static TaxTariffId New() => From(Guid.CreateVersion7());
}
