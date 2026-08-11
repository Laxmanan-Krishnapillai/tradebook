using System.Text.Json;
using Tradebook.Core.Analytics;

namespace Tradebook.UnitTests;

public sealed class SemanticQueryCompilerTests
{
    private readonly SemanticQueryCompiler _compiler = new(new SemanticModelLoader());

    [Fact]
    public void QuarterGranularityIsEmittedInSelectAndGroupBy()
    {
        var query = _compiler.Compile(
            new JsonQueryAst(
                "delivery_pnl_analytics",
                ["revenue_eur"],
                null,
                null,
                [new TimeDimensionQuery("supply_month", "quarter", null)],
                null,
                null,
                null,
                null
            )
        );

        Assert.Equal(
            2,
            query.SqlText.Split("date_trunc('quarter'", StringSplitOptions.None).Length - 1
        );
        Assert.Equal(["supply_month_quarter", "revenue_eur"], query.ResultColumnNames);
    }

    [Theory]
    [InlineData("unknown_dimension")]
    [InlineData("revenue_eur; drop table physical_deliveries")]
    public void UnknownDimensionsAreRejected(string member)
    {
        Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(
                new JsonQueryAst(
                    "delivery_pnl_analytics",
                    null,
                    null,
                    [member],
                    null,
                    null,
                    null,
                    null,
                    null
                )
            )
        );
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
    public void EveryAdvertisedFilterOperatorCompiles(
        FilterOperator filterOperator,
        string expectedSql
    )
    {
        var comparison =
            filterOperator
            is FilterOperator.GreaterThan
                or FilterOperator.GreaterThanOrEqual
                or FilterOperator.LessThan
                or FilterOperator.LessThanOrEqual;
        var set = filterOperator is FilterOperator.In or FilterOperator.NotIn;
        var member = comparison ? "revenue_eur" : "book_type";
        List<object> values;
        if (comparison)
        {
            values = [12.5m];
        }
        else if (set)
        {
            values = ["Sales", "Sourcing"];
        }
        else
        {
            values = ["Sales"];
        }

        var query = _compiler.Compile(
            new JsonQueryAst(
                "delivery_pnl_analytics",
                ["revenue_eur"],
                null,
                null,
                null,
                [new FilterQuery(member, filterOperator, values)],
                null,
                null,
                null
            )
        );

        Assert.Contains(expectedSql, query.SqlText, StringComparison.Ordinal);
    }

    [Fact]
    public void FilterValuesAreBoundAndContainsEscapesLikeWildcards()
    {
        const string injection = "'; DROP_TABLE physical_deliveries; --%\\";
        var query = _compiler.Compile(
            new JsonQueryAst(
                "delivery_pnl_analytics",
                ["revenue_eur"],
                null,
                null,
                null,
                [new FilterQuery("book_type", FilterOperator.Contains, [injection])],
                null,
                null,
                null
            )
        );

        Assert.Contains("ILIKE @p0 ESCAPE '\\'", query.SqlText, StringComparison.Ordinal);
        Assert.DoesNotContain(injection, query.SqlText, StringComparison.Ordinal);
        Assert.Equal("%'; DROP\\_TABLE physical\\_deliveries; --\\%\\\\%", query.Parameters["@p0"]);
    }

    [Theory]
    [InlineData(null, null, 500, 0)]
    [InlineData(-1, -1, 1, 0)]
    [InlineData(20_000, 7, 10_000, 7)]
    public void LimitAndOffsetAreClampedAndBound(
        int? limit,
        int? offset,
        int expectedLimit,
        int expectedOffset
    )
    {
        var query = _compiler.Compile(
            new JsonQueryAst(
                "delivery_pnl_analytics",
                ["revenue_eur"],
                null,
                null,
                null,
                null,
                null,
                limit,
                offset
            )
        );

        Assert.Contains("LIMIT @p0 OFFSET @p1", query.SqlText, StringComparison.Ordinal);
        Assert.Equal(expectedLimit, query.Parameters["@p0"]);
        Assert.Equal(expectedOffset, query.Parameters["@p1"]);
    }

    [Theory]
    [InlineData(FilterOperator.Equals)]
    [InlineData(FilterOperator.NotEquals)]
    [InlineData(FilterOperator.Contains)]
    [InlineData(FilterOperator.GreaterThan)]
    [InlineData(FilterOperator.GreaterThanOrEqual)]
    [InlineData(FilterOperator.LessThan)]
    [InlineData(FilterOperator.LessThanOrEqual)]
    public void ScalarFilterOperatorsRejectMultipleValues(FilterOperator filterOperator)
    {
        var member = filterOperator
            is FilterOperator.GreaterThan
                or FilterOperator.GreaterThanOrEqual
                or FilterOperator.LessThan
                or FilterOperator.LessThanOrEqual
            ? "revenue_eur"
            : "book_type";

        Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(
                new JsonQueryAst(
                    "delivery_pnl_analytics",
                    ["revenue_eur"],
                    null,
                    null,
                    null,
                    [new FilterQuery(member, filterOperator, [1, 2])],
                    null,
                    null,
                    null
                )
            )
        );
    }

    [Theory]
    [InlineData(FilterOperator.In)]
    [InlineData(FilterOperator.NotIn)]
    public void SetFilterOperatorsRejectEmptyValues(FilterOperator filterOperator)
    {
        Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(
                new JsonQueryAst(
                    "delivery_pnl_analytics",
                    ["revenue_eur"],
                    null,
                    null,
                    null,
                    [new FilterQuery("book_type", filterOperator, [])],
                    null,
                    null,
                    null
                )
            )
        );
    }

    [Fact]
    public void JsonElementFilterValuesAreNormalizedToDeclaredMemberTypes()
    {
        var query = _compiler.Compile(
            new JsonQueryAst(
                "delivery_pnl_analytics",
                ["revenue_eur"],
                null,
                null,
                null,
                [
                    new FilterQuery("revenue_eur", FilterOperator.GreaterThan, [Json("12.50")]),
                    new FilterQuery(
                        "supply_month",
                        FilterOperator.Equals,
                        [Json("\"2026-02-03\"")]
                    ),
                ],
                null,
                null,
                null
            )
        );

        Assert.Equal(12.50m, query.Parameters["@p0"]);
        Assert.Equal(new DateOnly(2026, 2, 3), query.Parameters["@p1"]);
    }

    [Theory]
    [InlineData("\"12.50\"", "revenue_eur", FilterOperator.Equals)]
    [InlineData("true", "revenue_eur", FilterOperator.Equals)]
    [InlineData("{}", "revenue_eur", FilterOperator.Equals)]
    [InlineData("\"not-a-date\"", "supply_month", FilterOperator.Equals)]
    public void FilterValuesMustMatchTheDeclaredMemberType(
        string json,
        string member,
        FilterOperator filterOperator
    )
    {
        Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(
                new JsonQueryAst(
                    "delivery_pnl_analytics",
                    ["revenue_eur"],
                    null,
                    null,
                    null,
                    [new FilterQuery(member, filterOperator, [Json(json)])],
                    null,
                    null,
                    null
                )
            )
        );
    }

    [Fact]
    public void StringComparisonsAndNumericContainsAreRejected()
    {
        Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(
                new JsonQueryAst(
                    "delivery_pnl_analytics",
                    ["revenue_eur"],
                    null,
                    null,
                    null,
                    [new FilterQuery("book_type", FilterOperator.GreaterThan, ["Sales"])],
                    null,
                    null,
                    null
                )
            )
        );
        Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(
                new JsonQueryAst(
                    "delivery_pnl_analytics",
                    ["revenue_eur"],
                    null,
                    null,
                    null,
                    [new FilterQuery("revenue_eur", FilterOperator.Contains, [10])],
                    null,
                    null,
                    null
                )
            )
        );
    }

    [Fact]
    public void UnknownFilterMembersAndEnumValuesAreRejected()
    {
        Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(
                new JsonQueryAst(
                    "delivery_pnl_analytics",
                    ["revenue_eur"],
                    null,
                    null,
                    null,
                    [new FilterQuery("unknown", FilterOperator.Equals, ["x"])],
                    null,
                    null,
                    null
                )
            )
        );
        Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(
                new JsonQueryAst(
                    "delivery_pnl_analytics",
                    ["revenue_eur"],
                    null,
                    null,
                    null,
                    [new FilterQuery("book_type", (FilterOperator)999, ["x"])],
                    null,
                    null,
                    null
                )
            )
        );
    }

    [Theory]
    [InlineData("hour")]
    [InlineData("quarter; drop table physical_deliveries")]
    public void GranularityMustBeWhitelisted(string granularity)
    {
        Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(
                new JsonQueryAst(
                    "delivery_pnl_analytics",
                    ["revenue_eur"],
                    null,
                    null,
                    [new TimeDimensionQuery("supply_month", granularity, null)],
                    null,
                    null,
                    null,
                    null
                )
            )
        );
    }

    [Fact]
    public void TimeDimensionRequiresADateMemberAndAValidOrderedRange()
    {
        Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(
                new JsonQueryAst(
                    "delivery_pnl_analytics",
                    ["revenue_eur"],
                    null,
                    null,
                    [new TimeDimensionQuery("book_type", "month", null)],
                    null,
                    null,
                    null,
                    null
                )
            )
        );
        Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(
                new JsonQueryAst(
                    "delivery_pnl_analytics",
                    ["revenue_eur"],
                    null,
                    null,
                    [new TimeDimensionQuery("supply_month", "month", ["2026-03-01", "2026-02-01"])],
                    null,
                    null,
                    null,
                    null
                )
            )
        );
        Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(
                new JsonQueryAst(
                    "delivery_pnl_analytics",
                    ["revenue_eur"],
                    null,
                    null,
                    [new TimeDimensionQuery("supply_month", "month", ["2026-01-01"])],
                    null,
                    null,
                    null,
                    null
                )
            )
        );
    }

    [Fact]
    public void TimestampDateRangesPreserveTimeAndOffsetInBoundValues()
    {
        var query = _compiler.Compile(
            new JsonQueryAst(
                "delivery_pnl_analytics",
                ["revenue_eur"],
                null,
                null,
                [
                    new TimeDimensionQuery(
                        "supply_month",
                        "month",
                        ["2026-01-01T00:00:00Z", "2026-08-05T23:59:59+02:00"]
                    ),
                ],
                null,
                null,
                null,
                null
            )
        );

        Assert.Equal(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            query.Parameters["@p0"]
        );
        Assert.Equal(
            new DateTimeOffset(2026, 8, 5, 23, 59, 59, TimeSpan.FromHours(2)),
            query.Parameters["@p1"]
        );
    }

    [Fact]
    public void CounterpartyDimensionUsesDeterministicTargetJoinChain()
    {
        var query = _compiler.Compile(
            new JsonQueryAst(
                "delivery_pnl_analytics",
                null,
                null,
                ["counterparty_segment"],
                null,
                null,
                null,
                null,
                null
            )
        );

        Assert.Contains("FROM physical_deliveries", query.SqlText, StringComparison.Ordinal);
        Assert.True(
            query.SqlText.IndexOf("JOIN contracts", StringComparison.Ordinal)
                < query.SqlText.IndexOf("JOIN counterparties", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void MetricsExpandOnlyValidatedMeasureReferences()
    {
        var query = _compiler.Compile(
            new JsonQueryAst(
                "delivery_pnl_analytics",
                null,
                ["avg_price_eur_mwh"],
                null,
                null,
                null,
                null,
                null,
                null
            )
        );

        Assert.Contains(
            "SUM(physical_deliveries.revenue_eur)",
            query.SqlText,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "NULLIF(SUM(physical_deliveries.volume_mwh), 0)",
            query.SqlText,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void DuplicateResultColumnsAreRejected()
    {
        Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(
                new JsonQueryAst(
                    "delivery_pnl_analytics",
                    ["revenue_eur", "revenue_eur"],
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null
                )
            )
        );
    }

    [Fact]
    public void SortMembersMustBeSelectedAndDirectionIsWhitelisted()
    {
        Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(
                new JsonQueryAst(
                    "delivery_pnl_analytics",
                    ["revenue_eur"],
                    null,
                    null,
                    null,
                    null,
                    [new SortQuery("volume_mwh", "desc")],
                    null,
                    null
                )
            )
        );
        Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(
                new JsonQueryAst(
                    "delivery_pnl_analytics",
                    ["revenue_eur"],
                    null,
                    null,
                    null,
                    null,
                    [new SortQuery("revenue_eur", "desc; drop table physical_deliveries")],
                    null,
                    null
                )
            )
        );
    }

    [Fact]
    public void UnknownModelsAndEmptyProjectionsAreRejected()
    {
        Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(
                new JsonQueryAst(
                    "not_a_model",
                    ["revenue_eur"],
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null
                )
            )
        );
        Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(
                new JsonQueryAst(
                    "delivery_pnl_analytics",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null
                )
            )
        );
    }

    [Fact]
    public void ExcessiveSelectedMembersAreRejectedBeforeSqlCompilation()
    {
        var exception = Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(Query(measures: Enumerable.Repeat("revenue_eur", 65).ToArray()))
        );

        Assert.Equal(
            "Query can contain at most 64 selected dimensions, measures and metrics.",
            exception.Message
        );
    }

    [Fact]
    public void MaximumSelectedMembersReachSemanticValidation()
    {
        var exception = Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(Query(measures: Enumerable.Repeat("revenue_eur", 64).ToArray()))
        );

        Assert.Equal("Result column 'revenue_eur' is selected more than once.", exception.Message);
    }

    [Fact]
    public void ExcessiveTimeDimensionsAreRejectedBeforeSqlCompilation()
    {
        var timeDimensions = Enumerable
            .Repeat(new TimeDimensionQuery("supply_month", "month", null), 17)
            .ToArray();

        var exception = Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(Query(timeDimensions: timeDimensions))
        );

        Assert.Equal("Query can contain at most 16 time dimensions.", exception.Message);
    }

    [Fact]
    public void MaximumTimeDimensionsReachSemanticValidation()
    {
        var timeDimensions = Enumerable
            .Repeat(new TimeDimensionQuery("supply_month", "month", null), 16)
            .ToArray();

        var exception = Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(Query(timeDimensions: timeDimensions))
        );

        Assert.Equal(
            "Result column 'supply_month_month' is selected more than once.",
            exception.Message
        );
    }

    [Fact]
    public void ExcessiveFiltersAreRejectedBeforeParameterBinding()
    {
        var filters = Enumerable
            .Range(0, 65)
            .Select(index => new FilterQuery("book_type", FilterOperator.Equals, [$"type-{index}"]))
            .ToArray();

        var exception = Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(Query(filters: filters))
        );

        Assert.Equal("Query can contain at most 64 filters.", exception.Message);
    }

    [Fact]
    public void ExcessiveValuesInOneFilterAreRejectedBeforeParameterBinding()
    {
        var values = Enumerable.Range(0, 257).Select(index => (object)$"type-{index}").ToArray();
        var filters = new[] { new FilterQuery("book_type", FilterOperator.In, values) };

        var exception = Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(Query(filters: filters))
        );

        Assert.Equal(
            "Query can contain at most 256 values for filter 'book_type'.",
            exception.Message
        );
    }

    [Fact]
    public void ExcessiveTotalFilterValuesAreRejectedBeforeParameterBinding()
    {
        var filters = Enumerable
            .Range(0, 5)
            .Select(filterIndex => new FilterQuery(
                "book_type",
                FilterOperator.In,
                Enumerable
                    .Range(0, 205)
                    .Select(valueIndex => (object)$"type-{filterIndex}-{valueIndex}")
                    .ToArray()
            ))
            .ToArray();

        var exception = Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(Query(filters: filters))
        );

        Assert.Equal("Query can contain at most 1024 total filter values.", exception.Message);
    }

    [Fact]
    public void ExcessiveSortsAreRejectedBeforeSqlCompilation()
    {
        var sorts = Enumerable.Repeat(new SortQuery("revenue_eur", "asc"), 17).ToArray();

        var exception = Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(Query(sorts: sorts))
        );

        Assert.Equal("Query can contain at most 16 sorts.", exception.Message);
    }

    [Fact]
    public void ExcessiveStringFilterValueIsRejectedBeforeEscapingAndBinding()
    {
        var filters = new[]
        {
            new FilterQuery("book_type", FilterOperator.Contains, [new string('x', 1_025)]),
        };

        var exception = Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(Query(filters: filters))
        );

        Assert.Equal(
            "Filter value for 'book_type' exceeds the maximum length of 1024.",
            exception.Message
        );
    }

    [Theory]
    [InlineData("model", "model name")]
    [InlineData("measure", "measure")]
    [InlineData("metric", "metric")]
    [InlineData("dimension", "dimension")]
    [InlineData("time-member", "time dimension member")]
    [InlineData("granularity", "time dimension granularity")]
    [InlineData("filter", "filter member")]
    [InlineData("sort-member", "sort member")]
    [InlineData("sort-direction", "sort direction")]
    public void ExcessiveIdentifierLengthsAreRejectedBeforeLookup(string field, string description)
    {
        var exception = Assert.Throws<SemanticValidationException>(() =>
            _compiler.Compile(QueryWithOversizedIdentifier(field))
        );

        Assert.Equal($"Query {description} cannot exceed 128 characters.", exception.Message);
    }

    [Fact]
    public void MaximumFilterSortAndStringValueLimitsCompile()
    {
        var filters = Enumerable
            .Range(0, 64)
            .Select(index => new FilterQuery(
                "book_type",
                FilterOperator.Equals,
                [index == 0 ? new string('x', 1_024) : $"type-{index}"]
            ))
            .ToArray();
        var sorts = Enumerable.Repeat(new SortQuery("revenue_eur", "asc"), 16).ToArray();

        var compiled = _compiler.Compile(
            Query(filters: filters, sorts: sorts, offset: int.MaxValue)
        );

        Assert.Equal(66, compiled.Parameters.Count);
        Assert.Equal(int.MaxValue, compiled.Parameters["@p65"]);
    }

    [Fact]
    public void MaximumPerFilterAndTotalValueLimitsCompile()
    {
        var values = Enumerable.Range(0, 256).Select(index => (object)$"type-{index}").ToArray();
        var filters = Enumerable
            .Range(0, 4)
            .Select(_ => new FilterQuery("book_type", FilterOperator.In, values))
            .ToArray();

        var compiled = _compiler.Compile(Query(filters: filters));

        Assert.Equal(1_026, compiled.Parameters.Count);
        Assert.Contains("@p1023", compiled.SqlText, StringComparison.Ordinal);
    }

    private static JsonQueryAst Query(
        string modelName = "delivery_pnl_analytics",
        IReadOnlyList<string>? measures = null,
        IReadOnlyList<string>? metrics = null,
        IReadOnlyList<string>? dimensions = null,
        IReadOnlyList<TimeDimensionQuery>? timeDimensions = null,
        IReadOnlyList<FilterQuery>? filters = null,
        IReadOnlyList<SortQuery>? sorts = null,
        int? offset = null
    ) =>
        new(
            modelName,
            measures ?? ["revenue_eur"],
            metrics,
            dimensions,
            timeDimensions,
            filters,
            sorts,
            null,
            offset
        );

    private static JsonQueryAst QueryWithOversizedIdentifier(string field)
    {
        var oversized = new string('x', 129);
        return field switch
        {
            "model" => Query(modelName: oversized),
            "measure" => Query(measures: [oversized]),
            "metric" => Query(metrics: [oversized]),
            "dimension" => Query(dimensions: [oversized]),
            "time-member" => Query(
                timeDimensions: [new TimeDimensionQuery(oversized, "month", null)]
            ),
            "granularity" => Query(
                timeDimensions: [new TimeDimensionQuery("supply_month", oversized, null)]
            ),
            "filter" => Query(
                filters: [new FilterQuery(oversized, FilterOperator.Equals, ["value"])]
            ),
            "sort-member" => Query(sorts: [new SortQuery(oversized, "asc")]),
            "sort-direction" => Query(sorts: [new SortQuery("revenue_eur", oversized)]),
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
        };
    }

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
