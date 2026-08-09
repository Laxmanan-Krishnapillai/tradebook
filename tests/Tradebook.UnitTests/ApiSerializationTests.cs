using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using Tradebook.Api.Serialization;
using Tradebook.Core.Analytics;

namespace Tradebook.UnitTests;

public sealed class ApiSerializationTests
{
    [Fact]
    public void MoneyConverterRoundTripsMaximumPrecisionAsAString()
    {
        var options = CreateOptions();

        var json = JsonSerializer.Serialize(18.12345678m, options);
        var roundTripped = JsonSerializer.Deserialize<decimal>(json, options);

        json.Should().Be("\"18.12345678\"");
        roundTripped.Should().Be(18.12345678m);
    }

    [Fact]
    public void MoneyConverterRejectsJsonNumbers()
    {
        var act = () => JsonSerializer.Deserialize<decimal>("18.12345678", CreateOptions());

        act.Should().Throw<JsonException>().WithMessage("Money must be a decimal string*");
    }

    [Fact]
    public void DomainEnumsSerializeAsStrings()
    {
        var options = CreateOptions();
        options.Converters.Add(new JsonStringEnumConverter());

        JsonSerializer.Serialize(FilterOperator.Equals, options).Should().Be("\"Equals\"");
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new MoneyJsonConverter());
        return options;
    }
}
