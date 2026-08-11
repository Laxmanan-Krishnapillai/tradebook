namespace Tradebook.Core.Analytics;

internal static class SemanticFilterBuilder
{
    public static void Add(IReadOnlyList<FilterQuery>? filters, SemanticCompilationContext context)
    {
        foreach (var filter in filters ?? [])
        {
            if (filter is null || string.IsNullOrWhiteSpace(filter.Member))
            {
                throw new SemanticValidationException("Filter member is required.");
            }

            var dimension = context.Model.Dimensions.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, filter.Member, StringComparison.Ordinal)
            );
            if (dimension is not null)
            {
                context.RequiredEntities.Add(dimension.Entity);
                context.WhereClauses.Add(
                    BuildClause(
                        SemanticSqlExpressions.Dimension(context.Model, dimension),
                        filter,
                        SemanticSqlExpressions.ValueKindFor(dimension),
                        context.Parameters
                    )
                );
                continue;
            }

            var measure = context.Model.Measures.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, filter.Member, StringComparison.Ordinal)
            );
            if (measure is not null)
            {
                context.RequiredEntities.Add(measure.Entity);
                context.HavingClauses.Add(
                    BuildClause(
                        SemanticSqlExpressions.Aggregate(context.Model, measure),
                        filter,
                        SemanticValueKind.Number,
                        context.Parameters
                    )
                );
                continue;
            }

            throw new SemanticValidationException($"Unknown filter member '{filter.Member}'.");
        }
    }

    private static string BuildClause(
        string target,
        FilterQuery filter,
        SemanticValueKind valueKind,
        ParameterBag parameters
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

        ValidateOperator(filter, valueKind);
        var values = filter
            .Values.Select(value => SemanticValueParser.Normalize(value, valueKind, filter.Member))
            .ToArray();

        return filter.Operator switch
        {
            FilterOperator.Equals => $"{target} = {parameters.Bind(values[0])}",
            FilterOperator.NotEquals => $"{target} <> {parameters.Bind(values[0])}",
            FilterOperator.GreaterThan => $"{target} > {parameters.Bind(values[0])}",
            FilterOperator.GreaterThanOrEqual => $"{target} >= {parameters.Bind(values[0])}",
            FilterOperator.LessThan => $"{target} < {parameters.Bind(values[0])}",
            FilterOperator.LessThanOrEqual => $"{target} <= {parameters.Bind(values[0])}",
            FilterOperator.Contains =>
                $"{target} ILIKE {parameters.Bind($"%{EscapeLikePattern((string)values[0])}%")} ESCAPE '\\'",
            FilterOperator.In =>
                $"{target} IN ({string.Join(", ", values.Select(parameters.Bind))})",
            FilterOperator.NotIn =>
                $"{target} NOT IN ({string.Join(", ", values.Select(parameters.Bind))})",
            _ => throw new SemanticValidationException(
                $"Unsupported filter operator '{filter.Operator}'."
            ),
        };
    }

    private static void ValidateOperator(FilterQuery filter, SemanticValueKind valueKind)
    {
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
    }

    private static string EscapeLikePattern(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
