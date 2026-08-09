using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tradebook.Api.Serialization;

/// <summary>
/// Reads and writes decimal monetary values as JSON strings so their precision is preserved.
/// </summary>
public sealed class MoneyJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        if (reader.TokenType is not JsonTokenType.String)
        {
            throw new JsonException("Money must be a decimal string, not a JSON number.");
        }

        return decimal.Parse(
            reader.GetString()!,
            NumberStyles.Number,
            CultureInfo.InvariantCulture
        );
    }

    public override void Write(
        Utf8JsonWriter writer,
        decimal value,
        JsonSerializerOptions options
    ) => writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
}
