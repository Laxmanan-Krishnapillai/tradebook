using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Ids;

internal static class IdValidation
{
    internal static Validation Validate(Guid value, string typeName) =>
        value == Guid.Empty ? Validation.Invalid($"{typeName} must not be empty.") : Validation.Ok;
}
