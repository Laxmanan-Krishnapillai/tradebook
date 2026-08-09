using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Ids;

[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct ContractId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(ContractId));

    public static ContractId New() => From(Guid.CreateVersion7());
}
