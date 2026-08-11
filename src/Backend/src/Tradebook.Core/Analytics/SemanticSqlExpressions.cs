namespace Tradebook.Core.Analytics;

internal static class SemanticSqlExpressions
{
    public static DimensionDefinition FindDimension(SemanticModelRoot model, string name)
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

    public static MeasureDefinition FindMeasure(SemanticModelRoot model, string name)
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

    public static string Dimension(SemanticModelRoot model, DimensionDefinition dimension)
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

    public static string Aggregate(SemanticModelRoot model, MeasureDefinition measure)
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

    public static string ExpandMetric(
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
                    return Aggregate(model, measure);
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

    public static SemanticValueKind ValueKindFor(DimensionDefinition dimension) =>
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
}
