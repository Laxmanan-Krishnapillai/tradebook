using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Tradebook.Core.Analytics;

public sealed class SemanticQueryCompiler(SemanticModelLoader loader)
{
    private static readonly Dictionary<string, string> Granularities = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["day"] = "day",
        ["week"] = "week",
        ["month"] = "month",
        ["quarter"] = "quarter",
        ["year"] = "year",
    };

    public CompiledSqlQuery Compile(JsonQueryAst ast)
    {
        ArgumentNullException.ThrowIfNull(ast);
        var state = new CompilationState(loader.GetModel(ast.ModelName).SemanticModel);

        AddDimensions(ast, state);
        AddTimeDimensions(ast, state);
        AddMeasures(ast, state);
        AddMetrics(ast, state);

        if (state.Select.Count == 0)
        {
            throw new SemanticValidationException(
                "Query selects no dimensions, measures or metrics."
            );
        }

        AddFilters(ast, state);
        return new CompiledSqlQuery
        {
            SqlText = BuildSql(ast, state),
            Parameters = state.Parameters.Parameters,
            ResultColumnNames = state.Columns,
        };
    }

    private static void AddDimensions(JsonQueryAst ast, CompilationState state)
    {
        foreach (var name in ast.Dimensions ?? [])
        {
            var dimension = FindDimension(state.Model, name);
            state.RequiredEntities.Add(dimension.Entity);
            var sql = DimensionSql(state.Model, dimension);
            AddProjection(
                dimension.Name,
                $"{sql} AS {dimension.Name}",
                state.Select,
                state.Columns,
                state.ColumnSet
            );
            state.GroupBy.Add(sql);
        }
    }

    private static void AddTimeDimensions(JsonQueryAst ast, CompilationState state)
    {
        foreach (var time in ast.TimeDimensions ?? [])
        {
            if (time is null)
            {
                throw new SemanticValidationException("Time dimension cannot be null.");
            }

            var dimension = FindDimension(state.Model, time.Dimension);
            if (!string.Equals(dimension.Type, "date", StringComparison.Ordinal))
            {
                throw new SemanticValidationException(
                    $"Dimension '{dimension.Name}' is not a date dimension."
                );
            }

            if (
                string.IsNullOrWhiteSpace(time.Granularity)
                || !Granularities.TryGetValue(time.Granularity, out var granularity)
            )
            {
                throw new SemanticValidationException($"Unknown granularity '{time.Granularity}'.");
            }

            if (
                dimension.Granularity is null
                || !dimension.Granularity.Contains(granularity, StringComparer.Ordinal)
            )
            {
                throw new SemanticValidationException(
                    $"Granularity '{granularity}' is not declared for dimension '{dimension.Name}'."
                );
            }

            state.RequiredEntities.Add(dimension.Entity);
            var sql = DimensionSql(state.Model, dimension);
            var bucket = $"date_trunc('{granularity}', {sql})";
            var alias = $"{dimension.Name}_{granularity}";
            AddProjection(
                alias,
                $"{bucket} AS {alias}",
                state.Select,
                state.Columns,
                state.ColumnSet
            );
            state.GroupBy.Add(bucket);

            if (time.DateRange is not null)
            {
                AddDateRange(time.DateRange, dimension.Name, sql, state);
            }
        }
    }

    private static void AddDateRange(
        string[] dateRange,
        string dimensionName,
        string sql,
        CompilationState state
    )
    {
        if (dateRange.Length != 2)
        {
            throw new SemanticValidationException(
                $"Time dimension '{dimensionName}' has an invalid date range."
            );
        }

        var start = ParseTemporalValue(dateRange[0], dimensionName);
        var end = ParseTemporalValue(dateRange[1], dimensionName);
        if (start.Comparable > end.Comparable)
        {
            throw new SemanticValidationException(
                $"Time dimension '{dimensionName}' has an inverted date range."
            );
        }

        state.Where.Add(
            $"{sql} >= {state.Parameters.Bind(start.DatabaseValue)} AND "
                + $"{sql} <= {state.Parameters.Bind(end.DatabaseValue)}"
        );
    }

    private static void AddMeasures(JsonQueryAst ast, CompilationState state)
    {
        foreach (var name in ast.Measures ?? [])
        {
            var measure = FindMeasure(state.Model, name);
            state.RequiredEntities.Add(measure.Entity);
            AddProjection(
                measure.Name,
                $"{AggregateSql(state.Model, measure)} AS {measure.Name}",
                state.Select,
                state.Columns,
                state.ColumnSet
            );
        }
    }

    private static void AddMetrics(JsonQueryAst ast, CompilationState state)
    {
        foreach (var name in ast.Metrics ?? [])
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new SemanticValidationException("Metric name is required.");
            }

            var metric =
                state.Model.Metrics.FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, name, StringComparison.Ordinal)
                ) ?? throw new SemanticValidationException($"Metric '{name}' not found.");
            var expression = ExpandMetric(state.Model, metric, state.RequiredEntities);
            AddProjection(
                metric.Name,
                $"{expression} AS {metric.Name}",
                state.Select,
                state.Columns,
                state.ColumnSet
            );
        }
    }

    private static void AddFilters(JsonQueryAst ast, CompilationState state)
    {
        foreach (var filter in ast.Filters ?? [])
        {
            if (filter is null || string.IsNullOrWhiteSpace(filter.Member))
            {
                throw new SemanticValidationException("Filter member is required.");
            }

            var dimension = state.Model.Dimensions.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, filter.Member, StringComparison.Ordinal)
            );
            if (dimension is not null)
            {
                state.RequiredEntities.Add(dimension.Entity);
                state.Where.Add(
                    BuildFilterClause(
                        DimensionSql(state.Model, dimension),
                        filter,
                        ValueKindFor(dimension),
                        state.Parameters
                    )
                );
                continue;
            }

            var measure = state.Model.Measures.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, filter.Member, StringComparison.Ordinal)
            );
            if (measure is not null)
            {
                state.RequiredEntities.Add(measure.Entity);
                state.Having.Add(
                    BuildFilterClause(
                        AggregateSql(state.Model, measure),
                        filter,
                        SemanticValueKind.Number,
                        state.Parameters
                    )
                );
                continue;
            }

            throw new SemanticValidationException($"Unknown filter member '{filter.Member}'.");
        }
    }

    private string BuildSql(JsonQueryAst ast, CompilationState state)
    {
        var target = state.Model.Entities.First(entity =>
            string.Equals(entity.Name, state.Model.TargetEntity, StringComparison.Ordinal)
        );
        var builder = new StringBuilder("SELECT\n  ");
        builder.Append(string.Join(",\n  ", state.Select));
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"FROM {target.Table}");

        AppendJoins(builder, target, state);

        if (state.Where.Count > 0)
        {
            builder.AppendLine("WHERE " + string.Join(" AND ", state.Where));
        }

        if (state.GroupBy.Count > 0)
        {
            builder.AppendLine("GROUP BY " + string.Join(", ", state.GroupBy));
        }

        if (state.Having.Count > 0)
        {
            builder.AppendLine("HAVING " + string.Join(" AND ", state.Having));
        }

        AppendSorts(builder, ast, state.ColumnSet);
        var limitParameter = state.Parameters.Bind(Math.Clamp(ast.Limit ?? 500, 1, 10_000));
        var offsetParameter = state.Parameters.Bind(Math.Max(ast.Offset ?? 0, 0));
        builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"LIMIT {limitParameter} OFFSET {offsetParameter}"
        );
        return builder.ToString();
    }

    private void AppendJoins(StringBuilder builder, EntityDefinition target, CompilationState state)
    {
        var emittedJoins = new HashSet<string>(StringComparer.Ordinal);
        foreach (
            var entity in state.Model.Entities.Where(entity =>
                state.RequiredEntities.Contains(entity.Name)
                && !string.Equals(entity.Name, target.Name, StringComparison.Ordinal)
            )
        )
        {
            foreach (var step in loader.JoinChainFor(state.Model.Name, entity.Name))
            {
                if (!emittedJoins.Add(step.Join.Name))
                {
                    continue;
                }

                var left = state.Model.Entities.First(candidate =>
                    string.Equals(candidate.Name, step.Join.LeftEntity, StringComparison.Ordinal)
                );
                var right = state.Model.Entities.First(candidate =>
                    string.Equals(candidate.Name, step.Join.RightEntity, StringComparison.Ordinal)
                );
                var newTable = state
                    .Model.Entities.First(candidate =>
                        string.Equals(candidate.Name, step.NewEntity, StringComparison.Ordinal)
                    )
                    .Table;
                builder.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"{JoinTypeForTraversal(step)} JOIN {newTable} "
                        + $"ON {left.Table}.{step.Join.LeftColumn} = {right.Table}.{step.Join.RightColumn}"
                );
            }
        }
    }

    private static void AppendSorts(
        StringBuilder builder,
        JsonQueryAst ast,
        HashSet<string> columnSet
    )
    {
        if (ast.Sorts is not { Count: > 0 })
        {
            return;
        }

        var sorts = new List<string>();
        foreach (var sort in ast.Sorts)
        {
            if (sort is null || !columnSet.Contains(sort.Member))
            {
                throw new SemanticValidationException(
                    $"Sort member '{sort?.Member}' is not a selected column of this query."
                );
            }

            var direction = sort.Direction?.ToLowerInvariant() switch
            {
                "asc" => "ASC",
                "desc" => "DESC",
                _ => throw new SemanticValidationException(
                    $"Invalid sort direction '{sort.Direction}'."
                ),
            };
            sorts.Add($"{sort.Member} {direction}");
        }

        builder.AppendLine("ORDER BY " + string.Join(", ", sorts));
    }

    private static void AddProjection(
        string alias,
        string sql,
        List<string> select,
        List<string> columns,
        HashSet<string> columnSet
    )
    {
        if (!columnSet.Add(alias))
        {
            throw new SemanticValidationException(
                $"Result column '{alias}' is selected more than once."
            );
        }

        select.Add(sql);
        columns.Add(alias);
    }

    private static DimensionDefinition FindDimension(SemanticModelRoot model, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new SemanticValidationException("Dimension name is required.");
        }

        return model.Dimensions.FirstOrDefault(dimension =>
                string.Equals(dimension.Name, name, StringComparison.Ordinal)
            )
            ?? throw new SemanticValidationException(
                $"Dimension '{name}' not found in semantic model."
            );
    }

    private static MeasureDefinition FindMeasure(SemanticModelRoot model, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new SemanticValidationException("Measure name is required.");
        }

        return model.Measures.FirstOrDefault(measure =>
                string.Equals(measure.Name, name, StringComparison.Ordinal)
            )
            ?? throw new SemanticValidationException(
                $"Measure '{name}' not found in semantic model."
            );
    }

    private static string DimensionSql(SemanticModelRoot model, DimensionDefinition dimension)
    {
        var table = model
            .Entities.First(entity =>
                string.Equals(entity.Name, dimension.Entity, StringComparison.Ordinal)
            )
            .Table;
        var column = $"{table}.{dimension.Sql}";
        if (dimension.JsonbKey is null)
        {
            return dimension.Type.Equals("string", StringComparison.Ordinal)
                ? $"CAST({column} AS text)"
                : column;
        }

        var extracted = $"{column} ->> '{dimension.JsonbKey}'";
        return dimension.Type switch
        {
            "string" => extracted,
            "number" => $"CAST({extracted} AS numeric)",
            "date" => $"CAST({extracted} AS date)",
            "boolean" => $"CAST({extracted} AS boolean)",
            _ => throw new SemanticValidationException(
                $"Dimension '{dimension.Name}' has unsupported type '{dimension.Type}'."
            ),
        };
    }

    private static string JoinTypeForTraversal(JoinChainStep step)
    {
        var joinType = step.Join.JoinType;
        if (string.Equals(step.NewEntity, step.Join.LeftEntity, StringComparison.Ordinal))
        {
            joinType = joinType switch
            {
                "left" => "right",
                "right" => "left",
                _ => joinType,
            };
        }

        return joinType.ToUpperInvariant();
    }

    private static string AggregateSql(SemanticModelRoot model, MeasureDefinition measure)
    {
        var column =
            $"{model.Entities.First(entity => string.Equals(entity.Name, measure.Entity, StringComparison.Ordinal)).Table}.{measure.Sql}";
        return measure.Type switch
        {
            "sum" => $"SUM({column})",
            "avg" => $"AVG({column})",
            "count" => $"COUNT({column})",
            "count_distinct" => $"COUNT(DISTINCT {column})",
            "min" => $"MIN({column})",
            "max" => $"MAX({column})",
            _ => throw new SemanticValidationException(
                $"Unsupported aggregation '{measure.Type}'."
            ),
        };
    }

    private static string BuildFilterClause(
        string target,
        FilterQuery filter,
        SemanticValueKind valueKind,
        ParameterBag bag
    )
    {
        if (filter.Values is not { Count: > 0 })
        {
            throw new SemanticValidationException($"Filter on '{filter.Member}' has no values.");
        }

        var setOperator = filter.Operator is FilterOperator.In or FilterOperator.NotIn;
        if (!setOperator && filter.Values.Count != 1)
        {
            throw new SemanticValidationException(
                $"Filter operator '{filter.Operator}' on '{filter.Member}' requires exactly one value; "
                    + "use In or NotIn for multiple values."
            );
        }

        if (filter.Operator == FilterOperator.Contains && valueKind != SemanticValueKind.String)
        {
            throw new SemanticValidationException(
                $"Filter operator 'Contains' requires a string member, but '{filter.Member}' is {valueKind.ToString().ToLowerInvariant()}."
            );
        }

        if (
            filter.Operator
                is FilterOperator.GreaterThan
                    or FilterOperator.GreaterThanOrEqual
                    or FilterOperator.LessThan
                    or FilterOperator.LessThanOrEqual
            && valueKind is not (SemanticValueKind.Number or SemanticValueKind.Date)
        )
        {
            throw new SemanticValidationException(
                $"Filter operator '{filter.Operator}' is not valid for '{filter.Member}'."
            );
        }

        var values = filter
            .Values.Select(value => NormalizeValue(value, valueKind, filter.Member))
            .ToArray();

        return filter.Operator switch
        {
            FilterOperator.Equals => $"{target} = {bag.Bind(values[0])}",
            FilterOperator.NotEquals => $"{target} <> {bag.Bind(values[0])}",
            FilterOperator.GreaterThan => $"{target} > {bag.Bind(values[0])}",
            FilterOperator.GreaterThanOrEqual => $"{target} >= {bag.Bind(values[0])}",
            FilterOperator.LessThan => $"{target} < {bag.Bind(values[0])}",
            FilterOperator.LessThanOrEqual => $"{target} <= {bag.Bind(values[0])}",
            FilterOperator.Contains =>
                $"{target} ILIKE {bag.Bind($"%{EscapeLikePattern((string)values[0])}%")} ESCAPE '\\'",
            FilterOperator.In => $"{target} IN ({string.Join(", ", values.Select(bag.Bind))})",
            FilterOperator.NotIn =>
                $"{target} NOT IN ({string.Join(", ", values.Select(bag.Bind))})",
            _ => throw new SemanticValidationException(
                $"Unsupported filter operator '{filter.Operator}'."
            ),
        };
    }

    private static object NormalizeValue(object value, SemanticValueKind kind, string member)
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
                SemanticValueKind.String when unwrapped is string text => text,
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
                SemanticValueKind.Date when unwrapped is string dateText => ParseTemporalValue(
                    dateText,
                    member
                ).DatabaseValue,
                _ => throw new SemanticValidationException(
                    $"Filter value for '{member}' is not a valid {kind.ToString().ToLowerInvariant()}."
                ),
            };
        }
        catch (Exception exception) when (exception is OverflowException or FormatException)
        {
            throw new SemanticValidationException(
                $"Filter value for '{member}' is not a valid {kind.ToString().ToLowerInvariant()}."
            );
        }
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

    private static ParsedTemporalValue ParseTemporalValue(string? value, string member)
    {
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

    private readonly record struct ParsedTemporalValue(
        object DatabaseValue,
        DateTimeOffset Comparable
    );

    private static string EscapeLikePattern(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static string ExpandMetric(
        SemanticModelRoot model,
        MetricDefinition metric,
        HashSet<string> requiredEntities
    )
    {
        try
        {
            return MetricExpressionParser.Rewrite(
                metric.Expression,
                name =>
                {
                    var measure = FindMeasure(model, name);
                    requiredEntities.Add(measure.Entity);
                    return AggregateSql(model, measure);
                }
            );
        }
        catch (FormatException exception)
        {
            throw new SemanticValidationException(
                $"Metric '{metric.Name}' has an invalid expression: {exception.Message}"
            );
        }
    }

    private static SemanticValueKind ValueKindFor(DimensionDefinition dimension) =>
        dimension.Type switch
        {
            "string" => SemanticValueKind.String,
            "number" => SemanticValueKind.Number,
            "date" => SemanticValueKind.Date,
            "boolean" => SemanticValueKind.Boolean,
            _ => throw new SemanticValidationException(
                $"Dimension '{dimension.Name}' has unsupported type '{dimension.Type}'."
            ),
        };

    private sealed class CompilationState(SemanticModelRoot model)
    {
        public SemanticModelRoot Model { get; } = model;

        public ParameterBag Parameters { get; } = new();

        public List<string> Select { get; } = [];

        public List<string> GroupBy { get; } = [];

        public List<string> Where { get; } = [];

        public List<string> Having { get; } = [];

        public List<string> Columns { get; } = [];

        public HashSet<string> ColumnSet { get; } = new(StringComparer.Ordinal);

        public HashSet<string> RequiredEntities { get; } = new(StringComparer.Ordinal);
    }

    private enum SemanticValueKind
    {
        String,
        Number,
        Date,
        Boolean,
    }
}
