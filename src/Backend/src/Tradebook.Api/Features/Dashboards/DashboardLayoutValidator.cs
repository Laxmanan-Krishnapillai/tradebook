using System.Text.Json;
using Tradebook.Core.Analytics;

namespace Tradebook.Api.Features.Dashboards;

internal static class DashboardLayoutValidator
{
    private static readonly HashSet<string> ChartTypes = new(StringComparer.Ordinal) { "KPI_CARD", "SPARK_LINE", "LINE", "AREA", "BAR", "STACKED_BAR", "SCATTER", "HEATMAP", "CANDLESTICK", "TABLE" };

    public static bool TryValidate(
        Guid id,
        long version,
        JsonElement layout,
        SemanticQueryCompiler semanticQueries,
        JsonSerializerOptions serializerOptions,
        out string error)
    {
        if (id == Guid.Empty || version < 0) { error = "dashboardId and version are invalid."; return false; }
        if (!Object(layout, ["dashboardId", "title", "version", "theme", "refreshRateMs", "gridLayout", "widgets"], ["dashboardId", "title", "description", "version", "theme", "refreshRateMs", "gridLayout", "widgets"], out error) ||
            !GuidValue(layout.GetProperty("dashboardId"), id) || !StringValue(layout.GetProperty("title")) || !Integer(layout.GetProperty("version"), version) ||
            (layout.TryGetProperty("description", out var description) && description.ValueKind != JsonValueKind.String) ||
            !OneOf(layout.GetProperty("theme"), "DARK", "LIGHT", "SYSTEM") ||
            !Integer(layout.GetProperty("refreshRateMs")) ||
            !Grid(layout.GetProperty("gridLayout"), out error) ||
            !Widgets(layout.GetProperty("widgets"), semanticQueries, serializerOptions, out error))
        { error = string.IsNullOrEmpty(error) ? "Dashboard layout does not match the dashboard contract." : error; return false; }
        error = string.Empty; return true;
    }

    private static bool Grid(JsonElement grid, out string error)
    {
        if (!Object(grid, ["columns", "rowHeight", "items"], ["columns", "rowHeight", "items"], out error) || !PositiveInteger(grid.GetProperty("columns")) || !PositiveInteger(grid.GetProperty("rowHeight")) || grid.GetProperty("items").ValueKind != JsonValueKind.Array) { error = string.IsNullOrEmpty(error) ? "gridLayout is invalid." : error; return false; }
        foreach (var item in grid.GetProperty("items").EnumerateArray())
            if (!Object(item, ["widgetId", "x", "y", "w", "h"], ["widgetId", "x", "y", "w", "h", "minW", "minH", "static"], out error) || !StringValue(item.GetProperty("widgetId")) || !Integer(item.GetProperty("x")) || !Integer(item.GetProperty("y")) || !PositiveInteger(item.GetProperty("w")) || !PositiveInteger(item.GetProperty("h")) || (item.TryGetProperty("minW", out var minW) && !PositiveInteger(minW)) || (item.TryGetProperty("minH", out var minH) && !PositiveInteger(minH)) || (item.TryGetProperty("static", out var isStatic) && !Boolean(isStatic))) { error = string.IsNullOrEmpty(error) ? "gridLayout.items is invalid." : error; return false; }
        error = string.Empty; return true;
    }

    private static bool Widgets(
        JsonElement widgets,
        SemanticQueryCompiler semanticQueries,
        JsonSerializerOptions serializerOptions,
        out string error)
    {
        if (widgets.ValueKind != JsonValueKind.Array) { error = "widgets must be an array."; return false; }
        foreach (var widget in widgets.EnumerateArray())
        {
            if (!Object(widget, ["id", "title", "chartType", "semanticModelRef", "queryAst", "visualEncodings"], ["id", "title", "chartType", "semanticModelRef", "queryAst", "visualEncodings", "styleOverrides"], out error) ||
                !StringValue(widget.GetProperty("id")) ||
                !StringValue(widget.GetProperty("title")) ||
                !ChartType(widget.GetProperty("chartType")) ||
                !StringValue(widget.GetProperty("semanticModelRef")) ||
                !Query(widget.GetProperty("queryAst"), out error) ||
                !Encodings(widget.GetProperty("visualEncodings"), out error) ||
                (widget.TryGetProperty("styleOverrides", out var style) && !Style(style, out error)))
            {
                error = string.IsNullOrEmpty(error) ? "widgets is invalid." : error;
                return false;
            }

            var chartType = widget.GetProperty("chartType").GetString()!;
            var encodings = widget.GetProperty("visualEncodings");
            if (chartType == "CANDLESTICK" && encodings.GetProperty("yAxis").GetArrayLength() != 4)
            {
                error = "CANDLESTICK widgets require exactly four yAxis bindings in open, high, low, close order.";
                return false;
            }

            var modelReference = widget.GetProperty("semanticModelRef").GetString()!;
            try
            {
                var ast = widget.GetProperty("queryAst").Deserialize<JsonQueryAst>(serializerOptions)
                    ?? throw new JsonException("queryAst is null.");
                if (!string.Equals(modelReference, ast.ModelName, StringComparison.Ordinal))
                {
                    error = "semanticModelRef must match queryAst.modelName.";
                    return false;
                }

                semanticQueries.Compile(ast);
            }
            catch (SemanticValidationException exception)
            {
                error = $"queryAst is invalid: {exception.Message}";
                return false;
            }
            catch (JsonException exception)
            {
                error = $"queryAst is invalid: {exception.Message}";
                return false;
            }
            catch (NotSupportedException exception)
            {
                error = $"queryAst is invalid: {exception.Message}";
                return false;
            }
        }
        error = string.Empty; return true;
    }

    private static bool Encodings(JsonElement value, out string error)
    {
        if (!Object(value, ["xAxis", "yAxis"], ["xAxis", "yAxis", "colorBy", "sizeBy", "tooltipFields"], out error) || !StringValue(value.GetProperty("xAxis")) || !NonEmptyStringArray(value.GetProperty("yAxis")) || (value.TryGetProperty("colorBy", out var color) && !StringValue(color)) || (value.TryGetProperty("sizeBy", out var size) && !StringValue(size)) || (value.TryGetProperty("tooltipFields", out var tooltips) && !NonEmptyStringArray(tooltips))) { error = string.IsNullOrEmpty(error) ? "visualEncodings is invalid." : error; return false; }
        error = string.Empty; return true;
    }

    private static bool Style(JsonElement value, out string error)
    {
        if (!Object(value, [], ["showLegend", "showGridlines", "strokeWidth", "opacity"], out error) || (value.TryGetProperty("showLegend", out var legend) && !Boolean(legend)) || (value.TryGetProperty("showGridlines", out var gridlines) && !Boolean(gridlines)) || (value.TryGetProperty("strokeWidth", out var stroke) && !NumberInRange(stroke, 0)) || (value.TryGetProperty("opacity", out var opacity) && !NumberInRange(opacity, 0, 1))) { error = string.IsNullOrEmpty(error) ? "styleOverrides is invalid." : error; return false; }
        error = string.Empty; return true;
    }

    private static bool Query(JsonElement value, out string error)
    {
        if (!Object(value, ["modelName"], ["modelName", "measures", "metrics", "dimensions", "timeDimensions", "filters", "sorts", "limit", "offset"], out error) || !StringValue(value.GetProperty("modelName")) || !OptionalStrings(value, "measures") || !OptionalStrings(value, "metrics") || !OptionalStrings(value, "dimensions") || !OptionalBoundedInteger(value, "limit", 1, 10_000) || !OptionalBoundedInteger(value, "offset", 0, int.MaxValue) || !TimeDimensions(value, out error) || !Filters(value, out error) || !Sorts(value, out error)) { error = string.IsNullOrEmpty(error) ? "queryAst is invalid." : error; return false; }
        error = string.Empty; return true;
    }

    private static bool TimeDimensions(JsonElement query, out string error)
    {
        if (!query.TryGetProperty("timeDimensions", out var values)) { error = string.Empty; return true; }
        if (values.ValueKind != JsonValueKind.Array) { error = "timeDimensions must be an array."; return false; }
        foreach (var value in values.EnumerateArray()) if (!Object(value, ["dimension", "granularity"], ["dimension", "granularity", "dateRange"], out error) || !StringValue(value.GetProperty("dimension")) || !OneOf(value.GetProperty("granularity"), "day", "week", "month", "quarter", "year") || (value.TryGetProperty("dateRange", out var range) && !(range.ValueKind == JsonValueKind.Array && range.GetArrayLength() == 2 && range.EnumerateArray().All(StringValue)))) return false;
        error = string.Empty; return true;
    }

    private static bool Filters(JsonElement query, out string error)
    {
        if (!query.TryGetProperty("filters", out var values)) { error = string.Empty; return true; }
        if (values.ValueKind != JsonValueKind.Array) { error = "filters must be an array."; return false; }
        foreach (var value in values.EnumerateArray()) if (!Object(value, ["member", "operator", "values"], ["member", "operator", "values"], out error) || !StringValue(value.GetProperty("member")) || !OneOf(value.GetProperty("operator"), "equals", "notEquals", "contains", "greaterThan", "greaterThanOrEqual", "lessThan", "lessThanOrEqual", "in", "notIn") || !PrimitiveArray(value.GetProperty("values"), requireValue: true)) return false;
        error = string.Empty; return true;
    }

    private static bool Sorts(JsonElement query, out string error)
    {
        if (!query.TryGetProperty("sorts", out var values)) { error = string.Empty; return true; }
        if (values.ValueKind != JsonValueKind.Array) { error = "sorts must be an array."; return false; }
        foreach (var value in values.EnumerateArray()) if (!Object(value, ["member", "direction"], ["member", "direction"], out error) || !StringValue(value.GetProperty("member")) || !OneOf(value.GetProperty("direction"), "asc", "desc")) return false;
        error = string.Empty; return true;
    }

    private static bool Object(JsonElement value, IReadOnlyCollection<string> required, IReadOnlyCollection<string> allowed, out string error)
    {
        if (value.ValueKind != JsonValueKind.Object) { error = "Expected an object."; return false; }
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject()) if (!names.Add(property.Name) || !allowed.Contains(property.Name)) { error = $"Unexpected property '{property.Name}'."; return false; }
        foreach (var name in required) if (!names.Contains(name)) { error = $"Missing required property '{name}'."; return false; }
        error = string.Empty; return true;
    }

    private static bool OptionalStrings(JsonElement value, string name) => !value.TryGetProperty(name, out var array) || NonEmptyStringArray(array);
    private static bool OptionalBoundedInteger(JsonElement value, string name, int minimum, int maximum) => !value.TryGetProperty(name, out var number) || BoundedInteger(number, minimum, maximum);
    private static bool StringValue(JsonElement value) => value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString());
    private static bool NonEmptyStringArray(JsonElement value) => value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 0 && value.EnumerateArray().All(StringValue);
    private static bool PrimitiveArray(JsonElement value, bool requireValue = false) => value.ValueKind == JsonValueKind.Array && (!requireValue || value.GetArrayLength() > 0) && value.EnumerateArray().All(item => item.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False);
    private static bool Boolean(JsonElement value) => value.ValueKind is JsonValueKind.True or JsonValueKind.False;
    private static bool Integer(JsonElement value, long? expected = null) => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) && number >= 0 && (!expected.HasValue || number == expected.Value);
    private static bool PositiveInteger(JsonElement value) => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) && number >= 1;
    private static bool BoundedInteger(JsonElement value, int minimum, int maximum) => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number >= minimum && number <= maximum;
    private static bool NumberInRange(JsonElement value, double minimum, double maximum = double.MaxValue) => value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number) && number >= minimum && number <= maximum;
    private static bool GuidValue(JsonElement value, Guid expected) => value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id) && id == expected;
    private static bool ChartType(JsonElement value) => value.ValueKind == JsonValueKind.String && ChartTypes.Contains(value.GetString()!);
    private static bool OneOf(JsonElement value, params string[] allowed) => value.ValueKind == JsonValueKind.String && allowed.Contains(value.GetString(), StringComparer.Ordinal);
}
