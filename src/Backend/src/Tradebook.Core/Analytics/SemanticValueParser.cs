using System.Globalization;
using System.Text.Json;

namespace Tradebook.Core.Analytics;

internal static class SemanticValueParser
{
    public static object Normalize(object value, SemanticValueKind kind, string member)
    {
        object? unwrapped = value;
        if (value is JsonElement element)
        {
            unwrapped = element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number when element.TryGetDecimal(out var number) => number,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            };
        }

        try
        {
            return kind switch
            {
                SemanticValueKind.String when unwrapped is string text => ValidateString(
                    text,
                    member
                ),
                SemanticValueKind.Number when IsNumber(unwrapped) => Convert.ToDecimal(
                    unwrapped,
                    CultureInfo.InvariantCulture
                ),
                SemanticValueKind.Boolean when unwrapped is bool boolean => boolean,
                SemanticValueKind.Date when unwrapped is DateOnly date => date,
                SemanticValueKind.Date when unwrapped is DateTime dateTime => new DateTimeOffset(
                    dateTime.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
                        : dateTime.ToUniversalTime()
                ),
                SemanticValueKind.Date when unwrapped is DateTimeOffset offset => offset,
                SemanticValueKind.Date when unwrapped is string dateText => ParseTemporal(
                    dateText,
                    member
                ).DatabaseValue,
                _ => throw InvalidValue(member, kind),
            };
        }
        catch (Exception exception) when (exception is OverflowException or FormatException)
        {
            throw InvalidValue(member, kind);
        }
    }

    public static ParsedTemporalValue ParseTemporal(string? value, string member)
    {
        if (value?.Length > SemanticQueryShapeValidator.MaxStringFilterLength)
        {
            throw new SemanticValidationException(
                $"Date value for '{member}' exceeds the maximum length of {SemanticQueryShapeValidator.MaxStringFilterLength}."
            );
        }

        if (
            !string.IsNullOrWhiteSpace(value)
            && DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date
            )
        )
        {
            return new ParsedTemporalValue(
                date,
                new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            );
        }

        if (
            !string.IsNullOrWhiteSpace(value)
            && DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                out var timestamp
            )
        )
        {
            return new ParsedTemporalValue(timestamp, timestamp.ToUniversalTime());
        }

        throw new SemanticValidationException($"Date value for '{member}' is invalid.");
    }

    private static string ValidateString(string value, string member)
    {
        if (value.Length > SemanticQueryShapeValidator.MaxStringFilterLength)
        {
            throw new SemanticValidationException(
                $"Filter value for '{member}' exceeds the maximum length of {SemanticQueryShapeValidator.MaxStringFilterLength}."
            );
        }

        return value;
    }

    private static bool IsNumber(object? value) =>
        value
            is byte
                or sbyte
                or short
                or ushort
                or int
                or uint
                or long
                or ulong
                or float
                or double
                or decimal;

    private static SemanticValidationException InvalidValue(
        string member,
        SemanticValueKind kind
    ) => new($"Filter value for '{member}' is not a valid {kind.ToString().ToLowerInvariant()}.");
}
