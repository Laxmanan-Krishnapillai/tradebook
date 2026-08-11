namespace Tradebook.Core.Analytics;

internal static class SemanticProjectionBuilder
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

    public static void Add(JsonQueryAst ast, SemanticCompilationContext context)
    {
        AddDimensions(ast.Dimensions, context);
        AddTimeDimensions(ast.TimeDimensions, context);
        AddMeasures(ast.Measures, context);
        AddMetrics(ast.Metrics, context);
    }

    private static void AddDimensions(
        IReadOnlyList<string>? names,
        SemanticCompilationContext context
    )
    {
        foreach (var name in names ?? [])
        {
            var dimension = SemanticSqlExpressions.FindDimension(context.Model, name);
            context.RequiredEntities.Add(dimension.Entity);
            var sql = SemanticSqlExpressions.Dimension(context.Model, dimension);
            context.AddProjection(dimension.Name, $"{sql} AS {dimension.Name}", sql);
        }
    }

    private static void AddTimeDimensions(
        IReadOnlyList<TimeDimensionQuery>? timeDimensions,
        SemanticCompilationContext context
    )
    {
        foreach (var time in timeDimensions ?? [])
        {
            if (time is null)
            {
                throw new SemanticValidationException("Time dimension cannot be null.");
            }

            var dimension = SemanticSqlExpressions.FindDimension(context.Model, time.Dimension);
            if (!string.Equals(dimension.Type, "date", StringComparison.Ordinal))
            {
                throw new SemanticValidationException(
                    $"Dimension '{dimension.Name}' is not a date dimension."
                );
            }

            var granularity = ResolveGranularity(time, dimension);
            context.RequiredEntities.Add(dimension.Entity);
            var sql = SemanticSqlExpressions.Dimension(context.Model, dimension);
            var bucket = $"date_trunc('{granularity}', {sql})";
            var alias = $"{dimension.Name}_{granularity}";
            context.AddProjection(alias, $"{bucket} AS {alias}", bucket);

            if (time.DateRange is not null)
            {
                AddDateRange(time.DateRange, dimension.Name, sql, context);
            }
        }
    }

    private static string ResolveGranularity(TimeDimensionQuery time, DimensionDefinition dimension)
    {
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

        return granularity;
    }

    private static void AddDateRange(
        string[] dateRange,
        string dimensionName,
        string sql,
        SemanticCompilationContext context
    )
    {
        if (dateRange.Length != 2)
        {
            throw new SemanticValidationException(
                $"Time dimension '{dimensionName}' has an invalid date range."
            );
        }

        var start = SemanticValueParser.ParseTemporal(dateRange[0], dimensionName);
        var end = SemanticValueParser.ParseTemporal(dateRange[1], dimensionName);
        if (start.Comparable > end.Comparable)
        {
            throw new SemanticValidationException(
                $"Time dimension '{dimensionName}' has an inverted date range."
            );
        }

        context.WhereClauses.Add(
            $"{sql} >= {context.Parameters.Bind(start.DatabaseValue)} AND "
                + $"{sql} <= {context.Parameters.Bind(end.DatabaseValue)}"
        );
    }

    private static void AddMeasures(
        IReadOnlyList<string>? names,
        SemanticCompilationContext context
    )
    {
        foreach (var name in names ?? [])
        {
            var measure = SemanticSqlExpressions.FindMeasure(context.Model, name);
            context.RequiredEntities.Add(measure.Entity);
            context.AddProjection(
                measure.Name,
                $"{SemanticSqlExpressions.Aggregate(context.Model, measure)} AS {measure.Name}"
            );
        }
    }

    private static void AddMetrics(IReadOnlyList<string>? names, SemanticCompilationContext context)
    {
        foreach (var name in names ?? [])
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new SemanticValidationException("Metric name is required.");
            }

            var metric =
                context.Model.Metrics.FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, name, StringComparison.Ordinal)
                ) ?? throw new SemanticValidationException($"Metric '{name}' not found.");
            var expression = SemanticSqlExpressions.ExpandMetric(
                context.Model,
                metric,
                context.RequiredEntities
            );
            context.AddProjection(metric.Name, $"{expression} AS {metric.Name}");
        }
    }
}
