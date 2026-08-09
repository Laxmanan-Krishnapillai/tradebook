using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Money;

[ValueObject<decimal>(
    conversions: Conversions.SystemTextJson,
    parsableForStrings: ParsableForStrings.GenerateNothing,
    parsableForPrimitives: ParsableForPrimitives.GenerateNothing
)]
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

    public static bool operator <(Price left, Price right) => left.Value < right.Value;

    public static bool operator >(Price left, Price right) => left.Value > right.Value;

    public static bool operator <=(Price left, Price right) => left.Value <= right.Value;

    public static bool operator >=(Price left, Price right) => left.Value >= right.Value;
}
