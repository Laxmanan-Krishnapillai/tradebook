using System.Globalization;
using System.Text;

namespace Tradebook.Core.Analytics;

internal static class SemanticSqlBuilder
{
    private const int DefaultLimit = 500;
    private const int MaxLimit = 10_000;

    public static string Build(
        JsonQueryAst ast,
        SemanticCompilationContext context,
        SemanticModelLoader loader
    )
    {
        var target = context.Model.Entities.First(entity =>
            string.Equals(entity.Name, context.Model.TargetEntity, StringComparison.Ordinal)
        );
        var builder = new StringBuilder("SELECT\n  ");
        builder.Append(string.Join(",\n  ", context.SelectClauses));
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"FROM {target.Table}");

        AppendJoins(builder, target, context, loader);
        AppendClauses(builder, "WHERE", " AND ", context.WhereClauses);
        AppendClauses(builder, "GROUP BY", ", ", context.GroupByClauses);
        AppendClauses(builder, "HAVING", " AND ", context.HavingClauses);
        AppendSorts(builder, ast.Sorts, context);

        var limitParameter = context.Parameters.Bind(
            Math.Clamp(ast.Limit ?? DefaultLimit, 1, MaxLimit)
        );
        var offsetParameter = context.Parameters.Bind(Math.Max(ast.Offset ?? 0, 0));
        builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"LIMIT {limitParameter} OFFSET {offsetParameter}"
        );
        return builder.ToString();
    }

    private static void AppendJoins(
        StringBuilder builder,
        EntityDefinition target,
        SemanticCompilationContext context,
        SemanticModelLoader loader
    )
    {
        var emittedJoins = new HashSet<string>(StringComparer.Ordinal);
        foreach (
            var entity in context.Model.Entities.Where(entity =>
                context.RequiredEntities.Contains(entity.Name)
                && !string.Equals(entity.Name, target.Name, StringComparison.Ordinal)
            )
        )
        {
            foreach (var step in loader.JoinChainFor(context.Model.Name, entity.Name))
            {
                if (!emittedJoins.Add(step.Join.Name))
                {
                    continue;
                }

                var left = context.Model.Entities.First(candidate =>
                    string.Equals(candidate.Name, step.Join.LeftEntity, StringComparison.Ordinal)
                );
                var right = context.Model.Entities.First(candidate =>
                    string.Equals(candidate.Name, step.Join.RightEntity, StringComparison.Ordinal)
                );
                var newTable = context
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

    private static void AppendClauses(
        StringBuilder builder,
        string keyword,
        string separator,
        List<string> clauses
    )
    {
        if (clauses.Count > 0)
        {
            builder.AppendLine(
                CultureInfo.InvariantCulture,
                $"{keyword} {string.Join(separator, clauses)}"
            );
        }
    }

    private static void AppendSorts(
        StringBuilder builder,
        IReadOnlyList<SortQuery>? requestedSorts,
        SemanticCompilationContext context
    )
    {
        if (requestedSorts is not { Count: > 0 })
        {
            return;
        }

        var sorts = new List<string>(requestedSorts.Count);
        foreach (var sort in requestedSorts)
        {
            if (sort is null || !context.IsSelectedColumn(sort.Member))
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
}
