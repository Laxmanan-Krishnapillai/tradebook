using System.Text.Json;

namespace Tradebook.Api.Features.Dashboards;

internal static class DashboardLayoutValidator
{
    private static readonly HashSet<string> ChartTypes = new(StringComparer.Ordinal) { "KPI_CARD", "SPARK_LINE", "LINE", "AREA", "BAR", "STACKED_BAR", "SCATTER", "HEATMAP", "CANDLESTICK", "TABLE" };

    public static bool TryValidate(Guid id, long version, JsonElement layout, out string error)
    {
        if (id == Guid.Empty || version < 0) { error = "dashboardId and version are invalid."; return false; }
        if (!Object(layout, ["dashboardId", "title", "version", "gridLayout", "widgets"], ["dashboardId", "title", "description", "version", "theme", "refreshRateMs", "gridLayout", "widgets"], out error) ||
            !GuidValue(layout.GetProperty("dashboardId"), id) || !StringValue(layout.GetProperty("title")) || !Integer(layout.GetProperty("version"), version) ||
            (layout.TryGetProperty("description", out var description) && !StringValue(description)) ||
            (layout.TryGetProperty("theme", out var theme) && !OneOf(theme, "DARK", "LIGHT", "SYSTEM")) ||
            (layout.TryGetProperty("refreshRateMs", out var refresh) && !Integer(refresh)) ||
            !Grid(layout.GetProperty("gridLayout"), out error) || !Widgets(layout.GetProperty("widgets"), out error))
        { error = string.IsNullOrEmpty(error) ? "Dashboard layout does not match the dashboard contract." : error; return false; }
        error = string.Empty; return true;
    }

    private static bool Grid(JsonElement grid, out string error)
    {
        if (!Object(grid, ["columns", "rowHeight", "items"], ["columns", "rowHeight", "items"], out error) || !Integer(grid.GetProperty("columns")) || !Integer(grid.GetProperty("rowHeight")) || grid.GetProperty("items").ValueKind != JsonValueKind.Array) { error = string.IsNullOrEmpty(error) ? "gridLayout is invalid." : error; return false; }
        foreach (var item in grid.GetProperty("items").EnumerateArray())
            if (!Object(item, ["widgetId", "x", "y", "w", "h"], ["widgetId", "x", "y", "w", "h", "minW", "minH", "static"], out error) || !StringValue(item.GetProperty("widgetId")) || !Integer(item.GetProperty("x")) || !Integer(item.GetProperty("y")) || !Integer(item.GetProperty("w")) || !Integer(item.GetProperty("h")) || (item.TryGetProperty("minW", out var minW) && !Integer(minW)) || (item.TryGetProperty("minH", out var minH) && !Integer(minH)) || (item.TryGetProperty("static", out var isStatic) && !Boolean(isStatic))) { error = string.IsNullOrEmpty(error) ? "gridLayout.items is invalid." : error; return false; }
        error = string.Empty; return true;
    }

    private static bool Widgets(JsonElement widgets, out string error)
    {
        if (widgets.ValueKind != JsonValueKind.Array) { error = "widgets must be an array."; return false; }
        foreach (var widget in widgets.EnumerateArray())
            if (!Object(widget, ["id", "title", "chartType", "semanticModelRef", "queryAst", "visualEncodings"], ["id", "title", "chartType", "semanticModelRef", "queryAst", "visualEncodings", "styleOverrides"], out error) || !StringValue(widget.GetProperty("id")) || !StringValue(widget.GetProperty("title")) || !ChartType(widget.GetProperty("chartType")) || !StringValue(widget.GetProperty("semanticModelRef")) || !Query(widget.GetProperty("queryAst"), out error) || !Encodings(widget.GetProperty("visualEncodings"), out error) || (widget.TryGetProperty("styleOverrides", out var style) && !Style(style, out error))) { error = string.IsNullOrEmpty(error) ? "widgets is invalid." : error; return false; }
        error = string.Empty; return true;
    }

    private static bool Encodings(JsonElement value, out string error)
    {
        if (!Object(value, ["xAxis", "yAxis"], ["xAxis", "yAxis", "colorBy", "sizeBy", "tooltipFields"], out error) || !StringValue(value.GetProperty("xAxis")) || !StringArray(value.GetProperty("yAxis")) || (value.TryGetProperty("colorBy", out var color) && !StringValue(color)) || (value.TryGetProperty("sizeBy", out var size) && !StringValue(size)) || (value.TryGetProperty("tooltipFields", out var tooltips) && !StringArray(tooltips))) { error = string.IsNullOrEmpty(error) ? "visualEncodings is invalid." : error; return false; }
        error = string.Empty; return true;
    }

    private static bool Style(JsonElement value, out string error)
    {
        if (!Object(value, [], ["showLegend", "showGridlines", "strokeWidth", "opacity"], out error) || (value.TryGetProperty("showLegend", out var legend) && !Boolean(legend)) || (value.TryGetProperty("showGridlines", out var gridlines) && !Boolean(gridlines)) || (value.TryGetProperty("strokeWidth", out var stroke) && stroke.ValueKind != JsonValueKind.Number) || (value.TryGetProperty("opacity", out var opacity) && opacity.ValueKind != JsonValueKind.Number)) { error = string.IsNullOrEmpty(error) ? "styleOverrides is invalid." : error; return false; }
        error = string.Empty; return true;
    }

    private static bool Query(JsonElement value, out string error)
    {
        if (!Object(value, ["modelName"], ["modelName", "measures", "metrics", "dimensions", "timeDimensions", "filters", "sorts", "limit", "offset"], out error) || !StringValue(value.GetProperty("modelName")) || !OptionalStrings(value, "measures") || !OptionalStrings(value, "metrics") || !OptionalStrings(value, "dimensions") || !OptionalInteger(value, "limit") || !OptionalInteger(value, "offset") || !TimeDimensions(value, out error) || !Filters(value, out error) || !Sorts(value, out error)) { error = string.IsNullOrEmpty(error) ? "queryAst is invalid." : error; return false; }
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
        foreach (var value in values.EnumerateArray()) if (!Object(value, ["member", "operator", "values"], ["member", "operator", "values"], out error) || !StringValue(value.GetProperty("member")) || !OneOf(value.GetProperty("operator"), "equals", "notEquals", "contains", "greaterThan", "greaterThanOrEqual", "lessThan", "lessThanOrEqual", "in", "notIn") || !PrimitiveArray(value.GetProperty("values"))) return false;
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

    private static bool OptionalStrings(JsonElement value, string name) => !value.TryGetProperty(name, out var array) || StringArray(array);
    private static bool OptionalInteger(JsonElement value, string name) => !value.TryGetProperty(name, out var number) || Integer(number);
    private static bool StringValue(JsonElement value) => value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString());
    private static bool StringArray(JsonElement value) => value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(StringValue);
    private static bool PrimitiveArray(JsonElement value) => value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(item => item.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False);
    private static bool Boolean(JsonElement value) => value.ValueKind is JsonValueKind.True or JsonValueKind.False;
    private static bool Integer(JsonElement value, long? expected = null) => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) && number >= 0 && (!expected.HasValue || number == expected.Value);
    private static bool GuidValue(JsonElement value, Guid expected) => value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id) && id == expected;
    private static bool ChartType(JsonElement value) => value.ValueKind == JsonValueKind.String && ChartTypes.Contains(value.GetString()!);
    private static bool OneOf(JsonElement value, params string[] allowed) => value.ValueKind == JsonValueKind.String && allowed.Contains(value.GetString(), StringComparer.Ordinal);
}
