using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Ids;

[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct AuditLogId
{
    private static Validation Validate(Guid value) =>
        IdValidation.Validate(value, nameof(AuditLogId));

    public static AuditLogId New() => From(Guid.CreateVersion7());
}
