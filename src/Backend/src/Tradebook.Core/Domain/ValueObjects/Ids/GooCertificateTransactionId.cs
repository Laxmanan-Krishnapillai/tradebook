using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Ids;

[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct GooCertificateTransactionId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(GooCertificateTransactionId));

    public static GooCertificateTransactionId New() => From(Guid.CreateVersion7());
}
