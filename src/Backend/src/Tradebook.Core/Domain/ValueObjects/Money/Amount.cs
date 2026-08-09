using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Money;

[ValueObject<decimal>(
    conversions: Conversions.SystemTextJson,
    parsableForStrings: ParsableForStrings.GenerateNothing,
    parsableForPrimitives: ParsableForPrimitives.GenerateNothing
)]
public readonly partial struct Amount
{
    private static Validation Validate(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.ToEven) == value
            ? Validation.Ok
            : Validation.Invalid("Amount scale must not exceed 4 decimal places.");

    public static bool operator <(Amount left, Amount right) => left.Value < right.Value;

    public static bool operator >(Amount left, Amount right) => left.Value > right.Value;

    public static bool operator <=(Amount left, Amount right) => left.Value <= right.Value;

    public static bool operator >=(Amount left, Amount right) => left.Value >= right.Value;
}
