using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Money;

[ValueObject<decimal>]
public readonly partial struct Price
{
    private static Validation Validate(decimal value)
    {
        if (value < 0m)
            return Validation.Invalid("Price must be non-negative.");

        return decimal.Round(value, 4, MidpointRounding.ToEven) == value
            ? Validation.Ok
            : Validation.Invalid("Price scale must not exceed 4 decimal places.");
    }
}
