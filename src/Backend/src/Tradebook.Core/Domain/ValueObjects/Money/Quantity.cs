using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Money;

[ValueObject<decimal>]
public readonly partial struct Quantity
{
    private static Validation Validate(decimal value) =>
        decimal.Round(value, 8, MidpointRounding.ToEven) == value
            ? Validation.Ok
            : Validation.Invalid("Quantity scale must not exceed 8 decimal places.");
}
