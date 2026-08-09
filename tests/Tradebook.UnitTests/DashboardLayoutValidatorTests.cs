using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Tradebook.Api.Features.Dashboards;
using Tradebook.Core.Analytics;

namespace Tradebook.UnitTests;

public sealed class DashboardLayoutValidatorTests
{
    private const string ContractError = "Dashboard layout does not match the dashboard contract.";
    private const string GridError = "gridLayout is invalid.";
    private const string GridItemError = "gridLayout.items is invalid.";
    private const string WidgetError = "widgets is invalid.";
    private const string QueryError = "queryAst is invalid.";
    private const string EncodingError = "visualEncodings is invalid.";
    private const string StyleError = "styleOverrides is invalid.";

    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private readonly SemanticQueryCompiler _compiler = new(new SemanticModelLoader());

    [Fact]
    public void AcceptsAFullyPopulatedLayoutAndClearsTheError()
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        layout["description"] = "Delivery analytics";
        var gridItem = GridItem(layout);
        gridItem["minW"] = 1;
        gridItem["minH"] = 1;
        gridItem["static"] = false;

        var query = Query(layout);
        query["metrics"] = new JsonArray("avg_price_eur_mwh");
        query["timeDimensions"] = new JsonArray(
            new JsonObject
            {
                ["dimension"] = "supply_month",
                ["granularity"] = "month",
                ["dateRange"] = new JsonArray("2026-01-01", "2026-12-31"),
            }
        );
        query["filters"] = new JsonArray(
            new JsonObject
            {
                ["member"] = "book_type",
                ["operator"] = "equals",
                ["values"] = new JsonArray("Sourcing"),
            }
        );
        query["sorts"] = new JsonArray(
            new JsonObject { ["member"] = "supply_month_month", ["direction"] = "asc" }
        );
        query["limit"] = 1;
        query["offset"] = 0;

        var encodings = Encodings(layout);
        encodings["colorBy"] = "book_type";
        encodings["sizeBy"] = "volume_mwh";
        encodings["tooltipFields"] = new JsonArray("revenue_eur");
        Widget(layout)["styleOverrides"] = new JsonObject
        {
            ["showLegend"] = true,
            ["showGridlines"] = false,
            ["strokeWidth"] = 0,
            ["opacity"] = 1,
        };

        AssertValid(dashboardId, layout);
    }

    [Fact]
    public void AcceptsEmptyWidgetAndGridItemCollections()
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        Grid(layout)["items"] = new JsonArray();
        layout["widgets"] = new JsonArray();

        AssertValid(dashboardId, layout);
    }

    [Theory]
    [InlineData("DARK")]
    [InlineData("LIGHT")]
    [InlineData("SYSTEM")]
    public void AcceptsEachExactThemeAndNonNegativeIntegerBoundaries(string theme)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        layout["theme"] = theme;
        layout["refreshRateMs"] = 0;
        layout["description"] = string.Empty;

        AssertValid(dashboardId, layout);
    }

    [Fact]
    public void AcceptsTheLargestSupportedRouteAndLayoutVersion()
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        layout["version"] = long.MaxValue;

        AssertValid(dashboardId, layout, long.MaxValue);
    }

    [Fact]
    public void RejectsAnEmptyRouteDashboardIdIndependentlyOfVersion()
    {
        var layout = Layout(Guid.NewGuid());

        AssertInvalid(Guid.Empty, layout, "dashboardId and version are invalid.");
    }

    [Fact]
    public void RejectsANegativeRouteVersionIndependentlyOfDashboardId()
    {
        var dashboardId = Guid.NewGuid();

        AssertInvalid(dashboardId, Layout(dashboardId), "dashboardId and version are invalid.", -1);
    }

    [Theory]
    [InlineData("mismatched-id")]
    [InlineData("malformed-id")]
    [InlineData("numeric-id")]
    [InlineData("mismatched-version")]
    [InlineData("negative-version")]
    [InlineData("fractional-version")]
    public void RejectsLayoutIdentityOrVersionThatDoesNotExactlyMatchTheRoute(string scenario)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        switch (scenario)
        {
            case "mismatched-id":
                layout["dashboardId"] = Guid.NewGuid().ToString();
                break;
            case "malformed-id":
                layout["dashboardId"] = "not-a-guid";
                break;
            case "numeric-id":
                layout["dashboardId"] = 7;
                break;
            case "mismatched-version":
                layout["version"] = 1;
                break;
            case "negative-version":
                layout["version"] = -1;
                break;
            case "fractional-version":
                layout["version"] = 0.5;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        AssertInvalid(dashboardId, layout, ContractError);
    }

    [Theory]
    [InlineData("dashboardId")]
    [InlineData("title")]
    [InlineData("version")]
    [InlineData("theme")]
    [InlineData("refreshRateMs")]
    [InlineData("gridLayout")]
    [InlineData("widgets")]
    public void RejectsEachMissingRequiredRootProperty(string property)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        layout.Remove(property);

        AssertInvalid(dashboardId, layout, $"Missing required property '{property}'.");
    }

    [Fact]
    public void RejectsNonObjectUnknownAndDuplicateRootProperties()
    {
        var dashboardId = Guid.NewGuid();
        AssertInvalid(dashboardId, JsonValue.Create(42), "Expected an object.");

        var unknown = Layout(dashboardId);
        unknown["pluginRef"] = "runtime-plugin";
        AssertInvalid(dashboardId, unknown, "Unexpected property 'pluginRef'.");

        var json = Layout(dashboardId)
            .ToJsonString()
            .Replace(
                "\"title\":\"Dashboard\"",
                "\"title\":\"Dashboard\",\"title\":\"Duplicate\"",
                StringComparison.Ordinal
            );
        AssertInvalid(dashboardId, Element(json), "Unexpected property 'title'.");
    }

    [Theory]
    [InlineData("empty-title")]
    [InlineData("blank-title")]
    [InlineData("numeric-title")]
    [InlineData("non-string-description")]
    [InlineData("lowercase-theme")]
    [InlineData("unknown-theme")]
    [InlineData("numeric-theme")]
    [InlineData("negative-refresh")]
    [InlineData("fractional-refresh")]
    [InlineData("string-refresh")]
    public void RejectsInvalidRootScalarValues(string scenario)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        switch (scenario)
        {
            case "empty-title":
                layout["title"] = string.Empty;
                break;
            case "blank-title":
                layout["title"] = " \t ";
                break;
            case "numeric-title":
                layout["title"] = 1;
                break;
            case "non-string-description":
                layout["description"] = true;
                break;
            case "lowercase-theme":
                layout["theme"] = "system";
                break;
            case "unknown-theme":
                layout["theme"] = "AUTO";
                break;
            case "numeric-theme":
                layout["theme"] = 1;
                break;
            case "negative-refresh":
                layout["refreshRateMs"] = -1;
                break;
            case "fractional-refresh":
                layout["refreshRateMs"] = 0.5;
                break;
            case "string-refresh":
                layout["refreshRateMs"] = "30000";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        AssertInvalid(dashboardId, layout, ContractError);
    }

    [Theory]
    [InlineData("columns")]
    [InlineData("rowHeight")]
    [InlineData("items")]
    public void RejectsEachMissingGridProperty(string property)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        Grid(layout).Remove(property);

        AssertInvalid(dashboardId, layout, $"Missing required property '{property}'.");
    }

    [Theory]
    [InlineData("non-object")]
    [InlineData("unknown-property")]
    [InlineData("zero-columns")]
    [InlineData("fractional-columns")]
    [InlineData("zero-row-height")]
    [InlineData("non-array-items")]
    public void RejectsInvalidGridShapesAndPositiveIntegerBoundaries(string scenario)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        var expected = GridError;
        switch (scenario)
        {
            case "non-object":
                layout["gridLayout"] = 1;
                expected = "Expected an object.";
                break;
            case "unknown-property":
                Grid(layout)["breakpoints"] = 12;
                expected = "Unexpected property 'breakpoints'.";
                break;
            case "zero-columns":
                Grid(layout)["columns"] = 0;
                break;
            case "fractional-columns":
                Grid(layout)["columns"] = 1.5;
                break;
            case "zero-row-height":
                Grid(layout)["rowHeight"] = 0;
                break;
            case "non-array-items":
                Grid(layout)["items"] = new JsonObject();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        AssertInvalid(dashboardId, layout, expected);
    }

    [Theory]
    [InlineData("widgetId")]
    [InlineData("x")]
    [InlineData("y")]
    [InlineData("w")]
    [InlineData("h")]
    public void RejectsEachMissingGridItemProperty(string property)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        GridItem(layout).Remove(property);

        AssertInvalid(dashboardId, layout, $"Missing required property '{property}'.");
    }

    [Theory]
    [InlineData("non-object")]
    [InlineData("unknown-property")]
    [InlineData("blank-widget-id")]
    [InlineData("negative-x")]
    [InlineData("fractional-x")]
    [InlineData("negative-y")]
    [InlineData("zero-width")]
    [InlineData("zero-height")]
    [InlineData("zero-min-width")]
    [InlineData("zero-min-height")]
    [InlineData("non-boolean-static")]
    public void RejectsInvalidGridItemNestingAndBoundaries(string scenario)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        var item = GridItem(layout);
        var expected = GridItemError;
        switch (scenario)
        {
            case "non-object":
                Grid(layout)["items"] = new JsonArray { null };
                expected = "Expected an object.";
                break;
            case "unknown-property":
                item["z"] = 1;
                expected = "Unexpected property 'z'.";
                break;
            case "blank-widget-id":
                item["widgetId"] = " ";
                break;
            case "negative-x":
                item["x"] = -1;
                break;
            case "fractional-x":
                item["x"] = 0.5;
                break;
            case "negative-y":
                item["y"] = -1;
                break;
            case "zero-width":
                item["w"] = 0;
                break;
            case "zero-height":
                item["h"] = 0;
                break;
            case "zero-min-width":
                item["minW"] = 0;
                break;
            case "zero-min-height":
                item["minH"] = 0;
                break;
            case "non-boolean-static":
                item["static"] = "false";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        AssertInvalid(dashboardId, layout, expected);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AcceptsBothBooleanValuesAndMinimumOptionalGridSizes(bool isStatic)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        var item = GridItem(layout);
        item["minW"] = 1;
        item["minH"] = 1;
        item["static"] = isStatic;

        AssertValid(dashboardId, layout);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("title")]
    [InlineData("chartType")]
    [InlineData("semanticModelRef")]
    [InlineData("queryAst")]
    [InlineData("visualEncodings")]
    public void RejectsEachMissingWidgetProperty(string property)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        Widget(layout).Remove(property);

        AssertInvalid(dashboardId, layout, $"Missing required property '{property}'.");
    }

    [Theory]
    [InlineData("non-array")]
    [InlineData("non-object-widget")]
    [InlineData("unknown-widget-property")]
    [InlineData("blank-id")]
    [InlineData("blank-title")]
    [InlineData("blank-model")]
    [InlineData("unknown-chart")]
    [InlineData("wrong-case-chart")]
    public void RejectsInvalidWidgetShapesAndScalarValues(string scenario)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        var expected = WidgetError;
        switch (scenario)
        {
            case "non-array":
                layout["widgets"] = new JsonObject();
                expected = "widgets must be an array.";
                break;
            case "non-object-widget":
                layout["widgets"] = new JsonArray { null };
                expected = "Expected an object.";
                break;
            case "unknown-widget-property":
                Widget(layout)["pluginRef"] = "x";
                expected = "Unexpected property 'pluginRef'.";
                break;
            case "blank-id":
                Widget(layout)["id"] = " ";
                break;
            case "blank-title":
                Widget(layout)["title"] = string.Empty;
                break;
            case "blank-model":
                Widget(layout)["semanticModelRef"] = "\t";
                break;
            case "unknown-chart":
                Widget(layout)["chartType"] = "PIE";
                break;
            case "wrong-case-chart":
                Widget(layout)["chartType"] = "line";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        AssertInvalid(dashboardId, layout, expected);
    }

    [Theory]
    [InlineData("KPI_CARD")]
    [InlineData("SPARK_LINE")]
    [InlineData("LINE")]
    [InlineData("AREA")]
    [InlineData("BAR")]
    [InlineData("STACKED_BAR")]
    [InlineData("SCATTER")]
    [InlineData("HEATMAP")]
    [InlineData("CANDLESTICK")]
    [InlineData("TABLE")]
    public void AcceptsEveryRegisteredChartType(string chartType)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId, chartType);
        if (string.Equals(chartType, "CANDLESTICK", StringComparison.Ordinal))
            Encodings(layout)["yAxis"] = new JsonArray(
                "volume_mwh",
                "volume_mwh",
                "volume_mwh",
                "volume_mwh"
            );

        AssertValid(dashboardId, layout);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void RejectsCandlestickBindingsWithoutExactlyFourMembers(int bindingCount)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId, "CANDLESTICK");
        Encodings(layout)["yAxis"] = JsonSerializer.SerializeToNode(
            Enumerable.Repeat("volume_mwh", bindingCount).ToArray()
        );

        AssertInvalid(
            dashboardId,
            layout,
            "CANDLESTICK widgets require exactly four yAxis bindings in open, high, low, close order."
        );
    }

    [Fact]
    public void RejectsASemanticModelReferenceThatDiffersFromTheQueryModel()
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        Widget(layout)["semanticModelRef"] = "other_model";

        AssertInvalid(dashboardId, layout, "semanticModelRef must match queryAst.modelName.");
    }

    [Theory]
    [InlineData("modelName")]
    public void RejectsEachMissingQueryProperty(string property)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        Query(layout).Remove(property);

        AssertInvalid(dashboardId, layout, $"Missing required property '{property}'.");
    }

    [Theory]
    [InlineData("non-object")]
    [InlineData("unknown-property")]
    [InlineData("blank-model")]
    [InlineData("empty-measures")]
    [InlineData("non-array-metrics")]
    [InlineData("blank-dimension")]
    [InlineData("zero-limit")]
    [InlineData("limit-over-maximum")]
    [InlineData("fractional-limit")]
    [InlineData("negative-offset")]
    [InlineData("offset-over-int-maximum")]
    [InlineData("fractional-offset")]
    public void RejectsInvalidQueryShapeArraysAndNumericBoundaries(string scenario)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        var query = Query(layout);
        var expected = QueryError;
        switch (scenario)
        {
            case "non-object":
                Widget(layout)["queryAst"] = 1;
                expected = "Expected an object.";
                break;
            case "unknown-property":
                query["sql"] = "SELECT 1";
                expected = "Unexpected property 'sql'.";
                break;
            case "blank-model":
                query["modelName"] = " ";
                break;
            case "empty-measures":
                query["measures"] = new JsonArray();
                break;
            case "non-array-metrics":
                query["metrics"] = "avg_price_eur_mwh";
                break;
            case "blank-dimension":
                query["dimensions"] = new JsonArray(" ");
                break;
            case "zero-limit":
                query["limit"] = 0;
                break;
            case "limit-over-maximum":
                query["limit"] = 10_001;
                break;
            case "fractional-limit":
                query["limit"] = 1.5;
                break;
            case "negative-offset":
                query["offset"] = -1;
                break;
            case "offset-over-int-maximum":
                query["offset"] = (long)int.MaxValue + 1;
                break;
            case "fractional-offset":
                query["offset"] = 0.5;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        AssertInvalid(dashboardId, layout, expected);
    }

    [Theory]
    [InlineData("measure", 1, 0)]
    [InlineData("measure", 10000, 2147483647)]
    [InlineData("metric", 500, 0)]
    [InlineData("dimension", 500, 0)]
    public void AcceptsEachProjectionKindAndInclusiveQueryBounds(
        string projection,
        int limit,
        int offset
    )
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        var query = Query(layout);
        query.Remove("dimensions");
        query.Remove("measures");
        switch (projection)
        {
            case "measure":
                query["measures"] = new JsonArray("volume_mwh");
                break;
            case "metric":
                query["metrics"] = new JsonArray("avg_price_eur_mwh");
                break;
            case "dimension":
                query["dimensions"] = new JsonArray("supply_month");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(projection));
        }
        query["limit"] = limit;
        query["offset"] = offset;

        AssertValid(dashboardId, layout);
    }

    [Fact]
    public void RejectsAStructurallyValidQueryThatSelectsNothing()
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        Query(layout).Remove("dimensions");
        Query(layout).Remove("measures");

        AssertInvalid(
            dashboardId,
            layout,
            "queryAst is invalid: Query selects no dimensions, measures or metrics."
        );
    }

    [Fact]
    public void RejectsUnknownSemanticMembersAfterSchemaValidation()
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        Query(layout)["dimensions"] = new JsonArray("not_a_dimension");

        AssertInvalid(
            dashboardId,
            layout,
            "queryAst is invalid: Dimension 'not_a_dimension' not found in semantic model."
        );
    }

    [Theory]
    [InlineData("day")]
    [InlineData("week")]
    [InlineData("month")]
    [InlineData("quarter")]
    [InlineData("year")]
    public void AcceptsEachDeclaredTimeGranularity(string granularity)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        Query(layout)["timeDimensions"] = new JsonArray(
            new JsonObject
            {
                ["dimension"] = "supply_month",
                ["granularity"] = granularity,
                ["dateRange"] = new JsonArray("2026-01-01", "2026-12-31"),
            }
        );

        AssertValid(dashboardId, layout);
    }

    [Theory]
    [InlineData("non-array")]
    [InlineData("non-object-item")]
    [InlineData("missing-dimension")]
    [InlineData("missing-granularity")]
    [InlineData("unknown-property")]
    [InlineData("blank-dimension")]
    [InlineData("wrong-case-granularity")]
    [InlineData("short-date-range")]
    [InlineData("long-date-range")]
    [InlineData("non-string-date")]
    public void RejectsInvalidTimeDimensionNesting(string scenario)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        var query = Query(layout);
        var item = new JsonObject { ["dimension"] = "supply_month", ["granularity"] = "month" };
        query["timeDimensions"] = new JsonArray(item);
        var expected = QueryError;
        switch (scenario)
        {
            case "non-array":
                query["timeDimensions"] = new JsonObject();
                expected = "timeDimensions must be an array.";
                break;
            case "non-object-item":
                query["timeDimensions"] = new JsonArray { null };
                expected = "Expected an object.";
                break;
            case "missing-dimension":
                item.Remove("dimension");
                expected = "Missing required property 'dimension'.";
                break;
            case "missing-granularity":
                item.Remove("granularity");
                expected = "Missing required property 'granularity'.";
                break;
            case "unknown-property":
                item["timezone"] = "UTC";
                expected = "Unexpected property 'timezone'.";
                break;
            case "blank-dimension":
                item["dimension"] = " ";
                break;
            case "wrong-case-granularity":
                item["granularity"] = "Month";
                break;
            case "short-date-range":
                item["dateRange"] = new JsonArray("2026-01-01");
                break;
            case "long-date-range":
                item["dateRange"] = new JsonArray("2026-01-01", "2026-06-01", "2026-12-31");
                break;
            case "non-string-date":
                item["dateRange"] = new JsonArray("2026-01-01", 20261231);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        AssertInvalid(dashboardId, layout, expected);
    }

    [Theory]
    [InlineData(
        "inverted",
        "queryAst is invalid: Time dimension 'supply_month' has an inverted date range."
    )]
    [InlineData("invalid-date", "queryAst is invalid: Date value for 'supply_month' is invalid.")]
    [InlineData(
        "non-date-dimension",
        "queryAst is invalid: Dimension 'contract_instance_id' is not a date dimension."
    )]
    public void RejectsSemanticallyInvalidTimeDimensions(string scenario, string expected)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        var item = new JsonObject
        {
            ["dimension"] = "supply_month",
            ["granularity"] = "month",
            ["dateRange"] = new JsonArray("2026-01-01", "2026-12-31"),
        };
        Query(layout)["timeDimensions"] = new JsonArray(item);
        switch (scenario)
        {
            case "inverted":
                item["dateRange"] = new JsonArray("2026-12-31", "2026-01-01");
                break;
            case "invalid-date":
                item["dateRange"] = new JsonArray("not-a-date", "2026-12-31");
                break;
            case "non-date-dimension":
                item["dimension"] = "contract_instance_id";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        AssertInvalid(dashboardId, layout, expected);
    }

    [Theory]
    [InlineData("equals")]
    [InlineData("notEquals")]
    [InlineData("contains")]
    [InlineData("greaterThan")]
    [InlineData("greaterThanOrEqual")]
    [InlineData("lessThan")]
    [InlineData("lessThanOrEqual")]
    [InlineData("in")]
    [InlineData("notIn")]
    public void AcceptsEachFilterOperator(string filterOperator)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        var isComparison =
            filterOperator
            is "greaterThan"
                or "greaterThanOrEqual"
                or "lessThan"
                or "lessThanOrEqual";
        var isSet = filterOperator is "in" or "notIn";
        JsonArray values;
        if (isComparison)
        {
            values = new JsonArray(1);
        }
        else if (isSet)
        {
            values = new JsonArray("Sourcing", "Sales");
        }
        else
        {
            values = new JsonArray("Sourcing");
        }

        Query(layout)["filters"] = new JsonArray(
            new JsonObject
            {
                ["member"] = isComparison ? "volume_mwh" : "book_type",
                ["operator"] = filterOperator,
                ["values"] = values,
            }
        );

        AssertValid(dashboardId, layout);
    }

    [Theory]
    [InlineData("non-array")]
    [InlineData("non-object-item")]
    [InlineData("missing-member")]
    [InlineData("missing-operator")]
    [InlineData("missing-values")]
    [InlineData("unknown-property")]
    [InlineData("blank-member")]
    [InlineData("unknown-operator")]
    [InlineData("empty-values")]
    [InlineData("non-array-values")]
    [InlineData("nested-value")]
    public void RejectsInvalidFilterNestingAndValueShapes(string scenario)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        var query = Query(layout);
        var item = new JsonObject
        {
            ["member"] = "book_type",
            ["operator"] = "equals",
            ["values"] = new JsonArray("Sourcing"),
        };
        query["filters"] = new JsonArray(item);
        var expected = QueryError;
        switch (scenario)
        {
            case "non-array":
                query["filters"] = new JsonObject();
                expected = "filters must be an array.";
                break;
            case "non-object-item":
                query["filters"] = new JsonArray { null };
                expected = "Expected an object.";
                break;
            case "missing-member":
                item.Remove("member");
                expected = "Missing required property 'member'.";
                break;
            case "missing-operator":
                item.Remove("operator");
                expected = "Missing required property 'operator'.";
                break;
            case "missing-values":
                item.Remove("values");
                expected = "Missing required property 'values'.";
                break;
            case "unknown-property":
                item["caseSensitive"] = true;
                expected = "Unexpected property 'caseSensitive'.";
                break;
            case "blank-member":
                item["member"] = " ";
                break;
            case "unknown-operator":
                item["operator"] = "between";
                break;
            case "empty-values":
                item["values"] = new JsonArray();
                break;
            case "non-array-values":
                item["values"] = "Sourcing";
                break;
            case "nested-value":
                item["values"] = new JsonArray(new JsonObject());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        AssertInvalid(dashboardId, layout, expected);
    }

    [Fact]
    public void RejectsAFilterWhenOnlySomeValuesArePrimitive()
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        Query(layout)["filters"] = new JsonArray(
            new JsonObject
            {
                ["member"] = "book_type",
                ["operator"] = "equals",
                ["values"] = new JsonArray("Sourcing", new JsonObject { ["nested"] = "value" }),
            }
        );

        AssertInvalid(dashboardId, layout, QueryError);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PrimitiveBooleanFilterValuesPassSchemaThenFailSemanticTypeValidation(bool value)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        Query(layout)["filters"] = new JsonArray(
            new JsonObject
            {
                ["member"] = "book_type",
                ["operator"] = "equals",
                ["values"] = new JsonArray(value),
            }
        );

        AssertInvalid(
            dashboardId,
            layout,
            "queryAst is invalid: Filter value for 'book_type' is not a valid string."
        );
    }

    [Fact]
    public void RejectsAStructurallyValidFilterWithAnUnknownMember()
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        Query(layout)["filters"] = new JsonArray(
            new JsonObject
            {
                ["member"] = "raw_sql",
                ["operator"] = "equals",
                ["values"] = new JsonArray("x"),
            }
        );

        AssertInvalid(dashboardId, layout, "queryAst is invalid: Unknown filter member 'raw_sql'.");
    }

    [Theory]
    [InlineData("asc")]
    [InlineData("desc")]
    public void AcceptsEachSortDirection(string direction)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        Query(layout)["sorts"] = new JsonArray(
            new JsonObject { ["member"] = "supply_month", ["direction"] = direction }
        );

        AssertValid(dashboardId, layout);
    }

    [Theory]
    [InlineData("non-array")]
    [InlineData("non-object-item")]
    [InlineData("missing-member")]
    [InlineData("missing-direction")]
    [InlineData("unknown-property")]
    [InlineData("blank-member")]
    [InlineData("wrong-case-direction")]
    public void RejectsInvalidSortNesting(string scenario)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        var query = Query(layout);
        var item = new JsonObject { ["member"] = "supply_month", ["direction"] = "asc" };
        query["sorts"] = new JsonArray(item);
        var expected = QueryError;
        switch (scenario)
        {
            case "non-array":
                query["sorts"] = new JsonObject();
                expected = "sorts must be an array.";
                break;
            case "non-object-item":
                query["sorts"] = new JsonArray { null };
                expected = "Expected an object.";
                break;
            case "missing-member":
                item.Remove("member");
                expected = "Missing required property 'member'.";
                break;
            case "missing-direction":
                item.Remove("direction");
                expected = "Missing required property 'direction'.";
                break;
            case "unknown-property":
                item["nulls"] = "last";
                expected = "Unexpected property 'nulls'.";
                break;
            case "blank-member":
                item["member"] = " ";
                break;
            case "wrong-case-direction":
                item["direction"] = "ASC";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        AssertInvalid(dashboardId, layout, expected);
    }

    [Fact]
    public void RejectsASortMemberThatIsNotSelected()
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        Query(layout)["sorts"] = new JsonArray(
            new JsonObject { ["member"] = "contract_name", ["direction"] = "asc" }
        );

        AssertInvalid(
            dashboardId,
            layout,
            "queryAst is invalid: Sort member 'contract_name' is not a selected column of this query."
        );
    }

    [Theory]
    [InlineData("xAxis")]
    [InlineData("yAxis")]
    public void RejectsEachMissingVisualEncodingProperty(string property)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        Encodings(layout).Remove(property);

        AssertInvalid(dashboardId, layout, $"Missing required property '{property}'.");
    }

    [Theory]
    [InlineData("non-object")]
    [InlineData("unknown-property")]
    [InlineData("blank-x")]
    [InlineData("empty-y")]
    [InlineData("non-array-y")]
    [InlineData("blank-y-member")]
    [InlineData("blank-color")]
    [InlineData("numeric-size")]
    [InlineData("empty-tooltips")]
    [InlineData("blank-tooltip")]
    public void RejectsInvalidVisualEncodingNestingAndStringArrays(string scenario)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        var encodings = Encodings(layout);
        var expected = EncodingError;
        switch (scenario)
        {
            case "non-object":
                Widget(layout)["visualEncodings"] = 1;
                expected = "Expected an object.";
                break;
            case "unknown-property":
                encodings["stackBy"] = "book_type";
                expected = "Unexpected property 'stackBy'.";
                break;
            case "blank-x":
                encodings["xAxis"] = " ";
                break;
            case "empty-y":
                encodings["yAxis"] = new JsonArray();
                break;
            case "non-array-y":
                encodings["yAxis"] = "volume_mwh";
                break;
            case "blank-y-member":
                encodings["yAxis"] = new JsonArray("volume_mwh", " ");
                break;
            case "blank-color":
                encodings["colorBy"] = string.Empty;
                break;
            case "numeric-size":
                encodings["sizeBy"] = 12;
                break;
            case "empty-tooltips":
                encodings["tooltipFields"] = new JsonArray();
                break;
            case "blank-tooltip":
                encodings["tooltipFields"] = new JsonArray(" ");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        AssertInvalid(dashboardId, layout, expected);
    }

    [Theory]
    [InlineData("non-object")]
    [InlineData("unknown-property")]
    [InlineData("non-boolean-legend")]
    [InlineData("non-boolean-gridlines")]
    [InlineData("negative-stroke")]
    [InlineData("string-stroke")]
    [InlineData("negative-opacity")]
    [InlineData("opacity-over-one")]
    [InlineData("string-opacity")]
    public void RejectsInvalidStyleShapesTypesAndNumericBoundaries(string scenario)
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        var style = new JsonObject();
        Widget(layout)["styleOverrides"] = style;
        var expected = StyleError;
        switch (scenario)
        {
            case "non-object":
                Widget(layout)["styleOverrides"] = 1;
                expected = "Expected an object.";
                break;
            case "unknown-property":
                style["animation"] = true;
                expected = "Unexpected property 'animation'.";
                break;
            case "non-boolean-legend":
                style["showLegend"] = "true";
                break;
            case "non-boolean-gridlines":
                style["showGridlines"] = 0;
                break;
            case "negative-stroke":
                style["strokeWidth"] = -0.01;
                break;
            case "string-stroke":
                style["strokeWidth"] = "1";
                break;
            case "negative-opacity":
                style["opacity"] = -0.01;
                break;
            case "opacity-over-one":
                style["opacity"] = 1.01;
                break;
            case "string-opacity":
                style["opacity"] = "1";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        AssertInvalid(dashboardId, layout, expected);
    }

    [Theory]
    [InlineData(false, false, 0.0)]
    [InlineData(true, true, 1.0)]
    public void AcceptsStyleBooleanAndOpacityBoundaries(
        bool showLegend,
        bool showGridlines,
        double opacity
    )
    {
        var dashboardId = Guid.NewGuid();
        var layout = Layout(dashboardId);
        Widget(layout)["styleOverrides"] = new JsonObject
        {
            ["showLegend"] = showLegend,
            ["showGridlines"] = showGridlines,
            ["strokeWidth"] = 0,
            ["opacity"] = opacity,
        };

        AssertValid(dashboardId, layout);
    }

    [Theory]
    [InlineData("null", "queryAst is invalid: queryAst is null.")]
    [InlineData("json", "queryAst is invalid: rejected JSON")]
    [InlineData("unsupported", "queryAst is invalid: unsupported query type")]
    public void FailsClosedWhenQueryDeserializationCannotProduceTheContract(
        string behavior,
        string expected
    )
    {
        var dashboardId = Guid.NewGuid();
        var options = CreateSerializerOptions();
        options.Converters.Insert(0, new FailingQueryConverter(behavior));

        if (string.Equals(behavior, "unsupported", StringComparison.Ordinal))
        {
            AssertInvalidStartsWith(dashboardId, Layout(dashboardId), expected, options);
            return;
        }

        AssertInvalid(dashboardId, Layout(dashboardId), expected, serializerOptions: options);
    }

    private void AssertValid(Guid dashboardId, JsonNode layout, long version = 0) =>
        AssertValid(dashboardId, Element(layout), version);

    private void AssertValid(Guid dashboardId, JsonElement layout, long version = 0)
    {
        var valid = DashboardLayoutValidator.TryValidate(
            dashboardId,
            version,
            layout,
            _compiler,
            SerializerOptions,
            out var error
        );

        Assert.True(valid, error);
        Assert.Equal(string.Empty, error);
    }

    private void AssertInvalid(
        Guid dashboardId,
        JsonNode layout,
        string expectedError,
        long version = 0,
        JsonSerializerOptions? serializerOptions = null
    ) => AssertInvalid(dashboardId, Element(layout), expectedError, version, serializerOptions);

    private void AssertInvalid(
        Guid dashboardId,
        JsonElement layout,
        string expectedError,
        long version = 0,
        JsonSerializerOptions? serializerOptions = null
    )
    {
        var valid = DashboardLayoutValidator.TryValidate(
            dashboardId,
            version,
            layout,
            _compiler,
            serializerOptions ?? SerializerOptions,
            out var error
        );

        Assert.False(valid);
        Assert.Equal(expectedError, error);
    }

    private void AssertInvalidStartsWith(
        Guid dashboardId,
        JsonNode layout,
        string expectedErrorPrefix,
        JsonSerializerOptions serializerOptions
    )
    {
        var valid = DashboardLayoutValidator.TryValidate(
            dashboardId,
            0,
            Element(layout),
            _compiler,
            serializerOptions,
            out var error
        );

        Assert.False(valid);
        Assert.StartsWith(expectedErrorPrefix, error, StringComparison.Ordinal);
    }

    private static JsonObject Layout(Guid dashboardId, string chartType = "LINE") =>
        new()
        {
            ["dashboardId"] = dashboardId.ToString(),
            ["title"] = "Dashboard",
            ["version"] = 0,
            ["theme"] = "SYSTEM",
            ["refreshRateMs"] = 30_000,
            ["gridLayout"] = new JsonObject
            {
                ["columns"] = 12,
                ["rowHeight"] = 30,
                ["items"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["widgetId"] = "chart-1",
                        ["x"] = 0,
                        ["y"] = 0,
                        ["w"] = 6,
                        ["h"] = 4,
                    },
                },
            },
            ["widgets"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "chart-1",
                    ["title"] = "Chart",
                    ["chartType"] = chartType,
                    ["semanticModelRef"] = "delivery_pnl_analytics",
                    ["queryAst"] = new JsonObject
                    {
                        ["modelName"] = "delivery_pnl_analytics",
                        ["dimensions"] = new JsonArray("supply_month"),
                        ["measures"] = new JsonArray("volume_mwh"),
                    },
                    ["visualEncodings"] = new JsonObject
                    {
                        ["xAxis"] = "supply_month",
                        ["yAxis"] = new JsonArray("volume_mwh"),
                    },
                },
            },
        };

    private static JsonObject Grid(JsonObject layout) => layout["gridLayout"]!.AsObject();

    private static JsonObject GridItem(JsonObject layout) => Grid(layout)["items"]![0]!.AsObject();

    private static JsonObject Widget(JsonObject layout) => layout["widgets"]![0]!.AsObject();

    private static JsonObject Query(JsonObject layout) => Widget(layout)["queryAst"]!.AsObject();

    private static JsonObject Encodings(JsonObject layout) =>
        Widget(layout)["visualEncodings"]!.AsObject();

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static JsonElement Element(JsonNode node) => Element(node.ToJsonString());

    private static JsonElement Element(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private sealed class FailingQueryConverter(string behavior) : JsonConverter<JsonQueryAst>
    {
        public override JsonQueryAst? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        )
        {
            if (string.Equals(behavior, "null", StringComparison.Ordinal))
            {
                reader.Skip();
                return null;
            }

            throw behavior switch
            {
                "json" => new JsonException("rejected JSON"),
                "unsupported" => new NotSupportedException("unsupported query type"),
                _ => new ArgumentOutOfRangeException(
                    nameof(typeToConvert),
                    behavior,
                    "Unknown converter behavior."
                ),
            };
        }

        public override void Write(
            Utf8JsonWriter writer,
            JsonQueryAst value,
            JsonSerializerOptions options
        ) => throw new NotSupportedException();
    }
}
