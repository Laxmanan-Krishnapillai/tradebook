using System.Text.Json;
using Tradebook.Core.Analytics;

namespace Tradebook.UnitTests;

public sealed class SemanticQueryCompilerTests
{
    private readonly SemanticQueryCompiler _compiler = new(new SemanticModelLoader());

    [Fact]
    public void Quarter_granularity_is_emitted_in_select_and_group_by()
    {
        var query = _compiler.Compile(new JsonQueryAst(
            "delivery_pnl_analytics",
            ["revenue_eur"],
            null,
            null,
            [new TimeDimensionQuery("supply_month", "quarter", null)],
            null,
            null,
            null,
            null));

        Assert.Equal(
            2,
            query.SqlText.Split("date_trunc('quarter'", StringSplitOptions.None).Length - 1);
        Assert.Equal(["supply_month_quarter", "revenue_eur"], query.ResultColumnNames);
    }

    [Theory]
    [InlineData("unknown_dimension")]
    [InlineData("revenue_eur; drop table physical_deliveries")]
    public void Unknown_dimensions_are_rejected(string member)
    {
        Assert.Throws<SemanticValidationException>(() => _compiler.Compile(new JsonQueryAst(
            "delivery_pnl_analytics", null, null, [member], null, null, null, null, null)));
    }

    [Theory]
    [InlineData(FilterOperator.Equals, " = @p0")]
    [InlineData(FilterOperator.NotEquals, " <> @p0")]
    [InlineData(FilterOperator.Contains, " ILIKE @p0")]
    [InlineData(FilterOperator.GreaterThan, " > @p0")]
    [InlineData(FilterOperator.GreaterThanOrEqual, " >= @p0")]
    [InlineData(FilterOperator.LessThan, " < @p0")]
    [InlineData(FilterOperator.LessThanOrEqual, " <= @p0")]
    [InlineData(FilterOperator.In, " IN (@p0, @p1)")]
    [InlineData(FilterOperator.NotIn, " NOT IN (@p0, @p1)")]
    public void Every_advertised_filter_operator_compiles(
        FilterOperator filterOperator,
        string expectedSql)
    {
        var comparison = filterOperator is FilterOperator.GreaterThan or
            FilterOperator.GreaterThanOrEqual or
            FilterOperator.LessThan or
            FilterOperator.LessThanOrEqual;
        var set = filterOperator is FilterOperator.In or FilterOperator.NotIn;
        var member = comparison ? "revenue_eur" : "book_type";
        List<object> values = comparison
            ? [12.5m]
            : set
                ? ["Sales", "Sourcing"]
                : ["Sales"];

        var query = _compiler.Compile(new JsonQueryAst(
            "delivery_pnl_analytics",
            ["revenue_eur"],
            null,
            null,
            null,
            [new FilterQuery(member, filterOperator, values)],
            null,
            null,
            null));

        Assert.Contains(expectedSql, query.SqlText);
    }

    [Fact]
    public void Filter_values_are_bound_and_contains_escapes_like_wildcards()
    {
        const string injection = "'; DROP_TABLE physical_deliveries; --%\\";
        var query = _compiler.Compile(new JsonQueryAst(
            "delivery_pnl_analytics",
            ["revenue_eur"],
            null,
            null,
            null,
            [new FilterQuery("book_type", FilterOperator.Contains, [injection])],
            null,
            null,
            null));

        Assert.Contains("ILIKE @p0 ESCAPE '\\'", query.SqlText);
        Assert.DoesNotContain(injection, query.SqlText);
        Assert.Equal("%'; DROP\\_TABLE physical\\_deliveries; --\\%\\\\%", query.Parameters["@p0"]);
    }

    [Theory]
    [InlineData(FilterOperator.Equals)]
    [InlineData(FilterOperator.NotEquals)]
    [InlineData(FilterOperator.Contains)]
    [InlineData(FilterOperator.GreaterThan)]
    [InlineData(FilterOperator.GreaterThanOrEqual)]
    [InlineData(FilterOperator.LessThan)]
    [InlineData(FilterOperator.LessThanOrEqual)]
    public void Scalar_filter_operators_reject_multiple_values(FilterOperator filterOperator)
    {
        var member = filterOperator is FilterOperator.GreaterThan or
            FilterOperator.GreaterThanOrEqual or
            FilterOperator.LessThan or
            FilterOperator.LessThanOrEqual
                ? "revenue_eur"
                : "book_type";

        Assert.Throws<SemanticValidationException>(() => _compiler.Compile(new JsonQueryAst(
            "delivery_pnl_analytics",
            ["revenue_eur"],
            null,
            null,
            null,
            [new FilterQuery(member, filterOperator, [1, 2])],
            null,
            null,
            null)));
    }

    [Theory]
    [InlineData(FilterOperator.In)]
    [InlineData(FilterOperator.NotIn)]
    public void Set_filter_operators_reject_empty_values(FilterOperator filterOperator)
    {
        Assert.Throws<SemanticValidationException>(() => _compiler.Compile(new JsonQueryAst(
            "delivery_pnl_analytics",
            ["revenue_eur"],
            null,
            null,
            null,
            [new FilterQuery("book_type", filterOperator, [])],
            null,
            null,
            null)));
    }

    [Fact]
    public void Json_element_filter_values_are_normalized_to_declared_member_types()
    {
        var query = _compiler.Compile(new JsonQueryAst(
            "delivery_pnl_analytics",
            ["revenue_eur"],
            null,
            null,
            null,
            [
                new FilterQuery("revenue_eur", FilterOperator.GreaterThan, [Json("12.50")]),
                new FilterQuery("supply_month", FilterOperator.Equals, [Json("\"2026-02-03\"")])
            ],
            null,
            null,
            null));

        Assert.Equal(12.50m, query.Parameters["@p0"]);
        Assert.Equal(new DateOnly(2026, 2, 3), query.Parameters["@p1"]);
    }

    [Theory]
    [InlineData("\"12.50\"", "revenue_eur", FilterOperator.Equals)]
    [InlineData("true", "revenue_eur", FilterOperator.Equals)]
    [InlineData("{}", "revenue_eur", FilterOperator.Equals)]
    [InlineData("\"not-a-date\"", "supply_month", FilterOperator.Equals)]
    public void Filter_values_must_match_the_declared_member_type(
        string json,
        string member,
        FilterOperator filterOperator)
    {
        Assert.Throws<SemanticValidationException>(() => _compiler.Compile(new JsonQueryAst(
            "delivery_pnl_analytics",
            ["revenue_eur"],
            null,
            null,
            null,
            [new FilterQuery(member, filterOperator, [Json(json)])],
            null,
            null,
            null)));
    }

    [Fact]
    public void String_comparisons_and_numeric_contains_are_rejected()
    {
        Assert.Throws<SemanticValidationException>(() => _compiler.Compile(new JsonQueryAst(
            "delivery_pnl_analytics", ["revenue_eur"], null, null, null,
            [new FilterQuery("book_type", FilterOperator.GreaterThan, ["Sales"])],
            null, null, null)));
        Assert.Throws<SemanticValidationException>(() => _compiler.Compile(new JsonQueryAst(
            "delivery_pnl_analytics", ["revenue_eur"], null, null, null,
            [new FilterQuery("revenue_eur", FilterOperator.Contains, [10])],
            null, null, null)));
    }

    [Fact]
    public void Unknown_filter_members_and_enum_values_are_rejected()
    {
        Assert.Throws<SemanticValidationException>(() => _compiler.Compile(new JsonQueryAst(
            "delivery_pnl_analytics", ["revenue_eur"], null, null, null,
            [new FilterQuery("unknown", FilterOperator.Equals, ["x"])],
            null, null, null)));
        Assert.Throws<SemanticValidationException>(() => _compiler.Compile(new JsonQueryAst(
            "delivery_pnl_analytics", ["revenue_eur"], null, null, null,
            [new FilterQuery("book_type", (FilterOperator)999, ["x"])],
            null, null, null)));
    }

    [Theory]
    [InlineData("hour")]
    [InlineData("quarter; drop table physical_deliveries")]
    public void Granularity_must_be_whitelisted(string granularity)
    {
        Assert.Throws<SemanticValidationException>(() => _compiler.Compile(new JsonQueryAst(
            "delivery_pnl_analytics", ["revenue_eur"], null, null,
            [new TimeDimensionQuery("supply_month", granularity, null)],
            null, null, null, null)));
    }

    [Fact]
    public void Time_dimension_requires_a_date_member_and_a_valid_ordered_range()
    {
        Assert.Throws<SemanticValidationException>(() => _compiler.Compile(new JsonQueryAst(
            "delivery_pnl_analytics", ["revenue_eur"], null, null,
            [new TimeDimensionQuery("book_type", "month", null)],
            null, null, null, null)));
        Assert.Throws<SemanticValidationException>(() => _compiler.Compile(new JsonQueryAst(
            "delivery_pnl_analytics", ["revenue_eur"], null, null,
            [new TimeDimensionQuery("supply_month", "month", ["2026-03-01", "2026-02-01"])],
            null, null, null, null)));
        Assert.Throws<SemanticValidationException>(() => _compiler.Compile(new JsonQueryAst(
            "delivery_pnl_analytics", ["revenue_eur"], null, null,
            [new TimeDimensionQuery("supply_month", "month", ["2026-01-01"])],
            null, null, null, null)));
    }

    [Fact]
    public void Timestamp_date_ranges_preserve_time_and_offset_in_bound_values()
    {
        var query = _compiler.Compile(new JsonQueryAst(
            "delivery_pnl_analytics", ["revenue_eur"], null, null,
            [new TimeDimensionQuery(
                "supply_month",
                "month",
                ["2026-01-01T00:00:00Z", "2026-08-05T23:59:59+02:00"])],
            null, null, null, null));

        Assert.Equal(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            query.Parameters["@p0"]);
        Assert.Equal(
            new DateTimeOffset(2026, 8, 5, 23, 59, 59, TimeSpan.FromHours(2)),
            query.Parameters["@p1"]);
    }

    [Fact]
    public void Counterparty_dimension_uses_deterministic_target_join_chain()
    {
        var query = _compiler.Compile(new JsonQueryAst(
            "delivery_pnl_analytics", null, null, ["counterparty_segment"],
            null, null, null, null, null));

        Assert.Contains("FROM physical_deliveries", query.SqlText);
        Assert.True(
            query.SqlText.IndexOf("JOIN contracts", StringComparison.Ordinal) <
            query.SqlText.IndexOf("JOIN counterparties", StringComparison.Ordinal));
    }

    [Fact]
    public void Metrics_expand_only_validated_measure_references()
    {
        var query = _compiler.Compile(new JsonQueryAst(
            "delivery_pnl_analytics", null, ["avg_price_eur_mwh"], null,
            null, null, null, null, null));

        Assert.Contains("SUM(physical_deliveries.revenue_eur)", query.SqlText);
        Assert.Contains("NULLIF(SUM(physical_deliveries.volume_mwh), 0)", query.SqlText);
    }

    [Fact]
    public void Duplicate_result_columns_are_rejected()
    {
        Assert.Throws<SemanticValidationException>(() => _compiler.Compile(new JsonQueryAst(
            "delivery_pnl_analytics", ["revenue_eur", "revenue_eur"],
            null, null, null, null, null, null, null)));
    }

    [Fact]
    public void Sort_members_must_be_selected_and_direction_is_whitelisted()
    {
        Assert.Throws<SemanticValidationException>(() => _compiler.Compile(new JsonQueryAst(
            "delivery_pnl_analytics", ["revenue_eur"], null, null, null, null,
            [new SortQuery("volume_mwh", "desc")], null, null)));
        Assert.Throws<SemanticValidationException>(() => _compiler.Compile(new JsonQueryAst(
            "delivery_pnl_analytics", ["revenue_eur"], null, null, null, null,
            [new SortQuery("revenue_eur", "desc; drop table physical_deliveries")], null, null)));
    }

    [Fact]
    public void Unknown_models_and_empty_projections_are_rejected()
    {
        Assert.Throws<SemanticValidationException>(() => _compiler.Compile(new JsonQueryAst(
            "not_a_model", ["revenue_eur"], null, null, null, null, null, null, null)));
        Assert.Throws<SemanticValidationException>(() => _compiler.Compile(new JsonQueryAst(
            "delivery_pnl_analytics", null, null, null, null, null, null, null, null)));
    }

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
