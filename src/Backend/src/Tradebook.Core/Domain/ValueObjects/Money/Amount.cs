using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Money;

[ValueObject<decimal>]
public readonly partial struct Amount
{
    private static Validation Validate(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.ToEven) == value
            ? Validation.Ok
            : Validation.Invalid("Amount scale must not exceed 4 decimal places.");
}
