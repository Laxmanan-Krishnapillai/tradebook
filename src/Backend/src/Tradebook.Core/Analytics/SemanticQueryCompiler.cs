using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Tradebook.Core.Analytics;

public sealed class SemanticValidationException(string message) : Exception(message);

public sealed class ParameterBag
{
    private int _counter;
    public Dictionary<string, object> Parameters { get; } = [];

    public string Bind(object value)
    {
        var name = $"@p{_counter++}";
        Parameters[name] = value;
        return name;
    }
}

public sealed class CompiledSqlQuery
{
    public required string SqlText { get; init; }
    public required Dictionary<string, object> Parameters { get; init; }
    public required List<string> ResultColumnNames { get; init; }
}

public sealed class SemanticQueryCompiler(SemanticModelLoader loader)
{
    private static readonly Dictionary<string, string> Granularities = new(
        new Dictionary<string, string>
        {
            ["day"] = "day",
            ["week"] = "week",
            ["month"] = "month",
            ["quarter"] = "quarter",
            ["year"] = "year"
        },
        StringComparer.OrdinalIgnoreCase);

    public CompiledSqlQuery Compile(JsonQueryAst ast)
    {
        ArgumentNullException.ThrowIfNull(ast);
        var model = loader.GetModel(ast.ModelName).SemanticModel;
        var bag = new ParameterBag();
        var select = new List<string>();
        var groupBy = new List<string>();
        var where = new List<string>();
        var having = new List<string>();
        var columns = new List<string>();
        var columnSet = new HashSet<string>(StringComparer.Ordinal);
        var requiredEntities = new HashSet<string>(StringComparer.Ordinal);

        foreach (var name in ast.Dimensions ?? [])
        {
            var dimension = FindDimension(model, name);
            requiredEntities.Add(dimension.Entity);
            var sql = DimensionSql(model, dimension);
            AddProjection(dimension.Name, $"{sql} AS {dimension.Name}", select, columns, columnSet);
            groupBy.Add(sql);
        }

        foreach (var time in ast.TimeDimensions ?? [])
        {
            if (time is null)
            {
                throw new SemanticValidationException("Time dimension cannot be null.");
            }

            var dimension = FindDimension(model, time.Dimension);
            if (!dimension.Type.Equals("date", StringComparison.Ordinal))
            {
                throw new SemanticValidationException(
                    $"Dimension '{dimension.Name}' is not a date dimension.");
            }

            if (string.IsNullOrWhiteSpace(time.Granularity) ||
                !Granularities.TryGetValue(time.Granularity, out var granularity))
            {
                throw new SemanticValidationException(
                    $"Unknown granularity '{time.Granularity}'.");
            }

            if (dimension.Granularity is null ||
                !dimension.Granularity.Contains(granularity, StringComparer.Ordinal))
            {
                throw new SemanticValidationException(
                    $"Granularity '{granularity}' is not declared for dimension '{dimension.Name}'.");
            }

            requiredEntities.Add(dimension.Entity);
            var sql = DimensionSql(model, dimension);
            var bucket = $"date_trunc('{granularity}', {sql})";
            var alias = $"{dimension.Name}_{granularity}";
            AddProjection(alias, $"{bucket} AS {alias}", select, columns, columnSet);
            groupBy.Add(bucket);

            if (time.DateRange is not null)
            {
                if (time.DateRange.Length != 2)
                {
                    throw new SemanticValidationException(
                        $"Time dimension '{dimension.Name}' has an invalid date range.");
                }

                var start = ParseTemporalValue(time.DateRange[0], dimension.Name);
                var end = ParseTemporalValue(time.DateRange[1], dimension.Name);
                if (start.Comparable > end.Comparable)
                {
                    throw new SemanticValidationException(
                        $"Time dimension '{dimension.Name}' has an inverted date range.");
                }

                where.Add(
                    $"{sql} >= {bag.Bind(start.DatabaseValue)} AND " +
                    $"{sql} <= {bag.Bind(end.DatabaseValue)}");
            }
        }

        foreach (var name in ast.Measures ?? [])
        {
            var measure = FindMeasure(model, name);
            requiredEntities.Add(measure.Entity);
            AddProjection(
                measure.Name,
                $"{AggregateSql(model, measure)} AS {measure.Name}",
                select,
                columns,
                columnSet);
        }

        foreach (var name in ast.Metrics ?? [])
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new SemanticValidationException("Metric name is required.");
            }

            var metric = model.Metrics.FirstOrDefault(candidate => candidate.Name == name)
                ?? throw new SemanticValidationException($"Metric '{name}' not found.");
            var expression = ExpandMetric(model, metric, requiredEntities);
            AddProjection(
                metric.Name,
                $"{expression} AS {metric.Name}",
                select,
                columns,
                columnSet);
        }

        if (select.Count == 0)
        {
            throw new SemanticValidationException(
                "Query selects no dimensions, measures or metrics.");
        }

        foreach (var filter in ast.Filters ?? [])
        {
            if (filter is null || string.IsNullOrWhiteSpace(filter.Member))
            {
                throw new SemanticValidationException("Filter member is required.");
            }

            var dimension = model.Dimensions.FirstOrDefault(candidate => candidate.Name == filter.Member);
            if (dimension is not null)
            {
                requiredEntities.Add(dimension.Entity);
                where.Add(BuildFilterClause(
                    DimensionSql(model, dimension),
                    filter,
                    ValueKindFor(dimension),
                    bag));
                continue;
            }

            var measure = model.Measures.FirstOrDefault(candidate => candidate.Name == filter.Member);
            if (measure is not null)
            {
                requiredEntities.Add(measure.Entity);
                having.Add(BuildFilterClause(
                    AggregateSql(model, measure),
                    filter,
                    SemanticValueKind.Number,
                    bag));
                continue;
            }

            throw new SemanticValidationException(
                $"Unknown filter member '{filter.Member}'.");
        }

        var target = model.Entities.First(entity => entity.Name == model.TargetEntity);
        var builder = new StringBuilder("SELECT\n  ");
        builder.Append(string.Join(",\n  ", select));
        builder.AppendLine();
        builder.AppendLine($"FROM {target.Table}");

        var emittedJoins = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entity in model.Entities.Where(
                     entity => requiredEntities.Contains(entity.Name) && entity.Name != target.Name))
        {
            foreach (var step in loader.JoinChainFor(model.Name, entity.Name))
            {
                if (!emittedJoins.Add(step.Join.Name))
                {
                    continue;
                }

                var left = model.Entities.First(candidate => candidate.Name == step.Join.LeftEntity);
                var right = model.Entities.First(candidate => candidate.Name == step.Join.RightEntity);
                var newTable = model.Entities.First(candidate => candidate.Name == step.NewEntity).Table;
                builder.AppendLine(
                    $"{JoinTypeForTraversal(step)} JOIN {newTable} " +
                    $"ON {left.Table}.{step.Join.LeftColumn} = {right.Table}.{step.Join.RightColumn}");
            }
        }

        if (where.Count > 0)
        {
            builder.AppendLine("WHERE " + string.Join(" AND ", where));
        }

        if (groupBy.Count > 0)
        {
            builder.AppendLine("GROUP BY " + string.Join(", ", groupBy));
        }

        if (having.Count > 0)
        {
            builder.AppendLine("HAVING " + string.Join(" AND ", having));
        }

        if (ast.Sorts is { Count: > 0 })
        {
            var sorts = new List<string>();
            foreach (var sort in ast.Sorts)
            {
                if (sort is null || !columnSet.Contains(sort.Member))
                {
                    throw new SemanticValidationException(
                        $"Sort member '{sort?.Member}' is not a selected column of this query.");
                }

                var direction = sort.Direction?.ToLowerInvariant() switch
                {
                    "asc" => "ASC",
                    "desc" => "DESC",
                    _ => throw new SemanticValidationException(
                        $"Invalid sort direction '{sort.Direction}'.")
                };
                sorts.Add($"{sort.Member} {direction}");
            }

            builder.AppendLine("ORDER BY " + string.Join(", ", sorts));
        }

        builder.AppendLine(
            $"LIMIT {Math.Clamp(ast.Limit ?? 500, 1, 10_000)} " +
            $"OFFSET {Math.Max(ast.Offset ?? 0, 0)}");

        return new CompiledSqlQuery
        {
            SqlText = builder.ToString(),
            Parameters = bag.Parameters,
            ResultColumnNames = columns
        };
    }

    private static void AddProjection(
        string alias,
        string sql,
        ICollection<string> select,
        ICollection<string> columns,
        ISet<string> columnSet)
    {
        if (!columnSet.Add(alias))
        {
            throw new SemanticValidationException(
                $"Result column '{alias}' is selected more than once.");
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

        return model.Dimensions.FirstOrDefault(dimension => dimension.Name == name)
            ?? throw new SemanticValidationException(
                $"Dimension '{name}' not found in semantic model.");
    }

    private static MeasureDefinition FindMeasure(SemanticModelRoot model, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new SemanticValidationException("Measure name is required.");
        }

        return model.Measures.FirstOrDefault(measure => measure.Name == name)
            ?? throw new SemanticValidationException(
                $"Measure '{name}' not found in semantic model.");
    }

    private static string DimensionSql(SemanticModelRoot model, DimensionDefinition dimension)
    {
        var table = model.Entities.First(entity => entity.Name == dimension.Entity).Table;
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
                $"Dimension '{dimension.Name}' has unsupported type '{dimension.Type}'.")
        };
    }

    private static string JoinTypeForTraversal(JoinChainStep step)
    {
        var joinType = step.Join.JoinType;
        if (step.NewEntity == step.Join.LeftEntity)
        {
            joinType = joinType switch
            {
                "left" => "right",
                "right" => "left",
                _ => joinType
            };
        }

        return joinType.ToUpperInvariant();
    }

    private static string AggregateSql(SemanticModelRoot model, MeasureDefinition measure)
    {
        var column =
            $"{model.Entities.First(entity => entity.Name == measure.Entity).Table}.{measure.Sql}";
        return measure.Type switch
        {
            "sum" => $"SUM({column})",
            "avg" => $"AVG({column})",
            "count" => $"COUNT({column})",
            "count_distinct" => $"COUNT(DISTINCT {column})",
            "min" => $"MIN({column})",
            "max" => $"MAX({column})",
            _ => throw new SemanticValidationException(
                $"Unsupported aggregation '{measure.Type}'.")
        };
    }

    private static string BuildFilterClause(
        string target,
        FilterQuery filter,
        SemanticValueKind valueKind,
        ParameterBag bag)
    {
        if (filter.Values is not { Count: > 0 })
        {
            throw new SemanticValidationException(
                $"Filter on '{filter.Member}' has no values.");
        }

        var setOperator = filter.Operator is FilterOperator.In or FilterOperator.NotIn;
        if (!setOperator && filter.Values.Count != 1)
        {
            throw new SemanticValidationException(
                $"Filter operator '{filter.Operator}' on '{filter.Member}' requires exactly one value; " +
                "use In or NotIn for multiple values.");
        }

        if (filter.Operator == FilterOperator.Contains && valueKind != SemanticValueKind.String)
        {
            throw new SemanticValidationException(
                $"Filter operator 'Contains' requires a string member, but '{filter.Member}' is {valueKind.ToString().ToLowerInvariant()}.");
        }

        if (filter.Operator is FilterOperator.GreaterThan or
            FilterOperator.GreaterThanOrEqual or
            FilterOperator.LessThan or
            FilterOperator.LessThanOrEqual &&
            valueKind is not (SemanticValueKind.Number or SemanticValueKind.Date))
        {
            throw new SemanticValidationException(
                $"Filter operator '{filter.Operator}' is not valid for '{filter.Member}'.");
        }

        var values = filter.Values
            .Select(value => NormalizeValue(value, valueKind, filter.Member))
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
            FilterOperator.In =>
                $"{target} IN ({string.Join(", ", values.Select(bag.Bind))})",
            FilterOperator.NotIn =>
                $"{target} NOT IN ({string.Join(", ", values.Select(bag.Bind))})",
            _ => throw new SemanticValidationException(
                $"Unsupported filter operator '{filter.Operator}'.")
        };
    }

    private static object NormalizeValue(
        object value,
        SemanticValueKind kind,
        string member)
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
                _ => null
            };
        }

        try
        {
            return kind switch
            {
                SemanticValueKind.String when unwrapped is string text => text,
                SemanticValueKind.Number when IsNumber(unwrapped) =>
                    Convert.ToDecimal(unwrapped, CultureInfo.InvariantCulture),
                SemanticValueKind.Boolean when unwrapped is bool boolean => boolean,
                SemanticValueKind.Date when unwrapped is DateOnly date => date,
                SemanticValueKind.Date when unwrapped is DateTime dateTime =>
                    new DateTimeOffset(
                        dateTime.Kind == DateTimeKind.Unspecified
                            ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
                            : dateTime.ToUniversalTime()),
                SemanticValueKind.Date when unwrapped is DateTimeOffset offset =>
                    offset,
                SemanticValueKind.Date when unwrapped is string dateText =>
                    ParseTemporalValue(dateText, member).DatabaseValue,
                _ => throw new SemanticValidationException(
                    $"Filter value for '{member}' is not a valid {kind.ToString().ToLowerInvariant()}.")
            };
        }
        catch (Exception exception) when (exception is OverflowException or FormatException)
        {
            throw new SemanticValidationException(
                $"Filter value for '{member}' is not a valid {kind.ToString().ToLowerInvariant()}.");
        }
    }

    private static bool IsNumber(object? value) => value is
        byte or sbyte or short or ushort or int or uint or long or ulong or
        float or double or decimal;

    private static ParsedTemporalValue ParseTemporalValue(string? value, string member)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return new ParsedTemporalValue(
                date,
                new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        }

        if (!string.IsNullOrWhiteSpace(value) &&
            DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                out var timestamp))
        {
            return new ParsedTemporalValue(timestamp, timestamp.ToUniversalTime());
        }

        throw new SemanticValidationException(
            $"Date value for '{member}' is invalid.");
    }

    private readonly record struct ParsedTemporalValue(
        object DatabaseValue,
        DateTimeOffset Comparable);

    private static string EscapeLikePattern(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    private static string ExpandMetric(
        SemanticModelRoot model,
        MetricDefinition metric,
        HashSet<string> requiredEntities)
    {
        try
        {
            return MetricExpressionParser.Rewrite(metric.Expression, name =>
            {
                var measure = FindMeasure(model, name);
                requiredEntities.Add(measure.Entity);
                return AggregateSql(model, measure);
            });
        }
        catch (FormatException exception)
        {
            throw new SemanticValidationException(
                $"Metric '{metric.Name}' has an invalid expression: {exception.Message}");
        }
    }

    private static SemanticValueKind ValueKindFor(DimensionDefinition dimension) => dimension.Type switch
    {
        "string" => SemanticValueKind.String,
        "number" => SemanticValueKind.Number,
        "date" => SemanticValueKind.Date,
        "boolean" => SemanticValueKind.Boolean,
        _ => throw new SemanticValidationException(
            $"Dimension '{dimension.Name}' has unsupported type '{dimension.Type}'.")
    };

    private enum SemanticValueKind
    {
        String,
        Number,
        Date,
        Boolean
    }
}
