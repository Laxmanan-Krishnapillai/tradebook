using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Ids;

[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct InvoiceLineItemId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(InvoiceLineItemId));

    public static InvoiceLineItemId New() => From(Guid.CreateVersion7());
}
