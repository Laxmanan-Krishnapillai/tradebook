namespace Tradebook.Core.Analytics;

internal static class SemanticQueryShapeValidator
{
    private const int MaxSelectedMembers = 64;
    private const int MaxTimeDimensions = 16;
    private const int MaxFilters = 64;
    private const int MaxValuesPerFilter = 256;
    private const int MaxTotalFilterValues = 1_024;
    private const int MaxSorts = 16;
    private const int MaxIdentifierLength = 128;

    internal const int MaxStringFilterLength = 1_024;

    public static void Validate(JsonQueryAst ast)
    {
        ValidateTextLength(ast.ModelName, "model name");
        ValidateMemberNames(ast.Measures, "measure");
        ValidateMemberNames(ast.Metrics, "metric");
        ValidateMemberNames(ast.Dimensions, "dimension");

        long selectedMemberCount =
            (long)Count(ast.Measures)
            + Count(ast.Metrics)
            + Count(ast.Dimensions)
            + Count(ast.TimeDimensions);
        RejectIfGreaterThan(
            selectedMemberCount,
            MaxSelectedMembers,
            "selected dimensions, measures and metrics"
        );
        ValidateTimeDimensions(ast.TimeDimensions);
        ValidateFilters(ast.Filters);
        ValidateSorts(ast.Sorts);
    }

    private static void ValidateFilters(IReadOnlyList<FilterQuery>? filters)
    {
        RejectIfGreaterThan(Count(filters), MaxFilters, "filters");
        long totalFilterValues = 0;
        foreach (var filter in (filters ?? []).OfType<FilterQuery>())
        {
            ValidateTextLength(filter.Member, "filter member");
            var valueCount = filter.Values?.Count ?? 0;
            RejectIfGreaterThan(
                valueCount,
                MaxValuesPerFilter,
                $"values for filter '{filter.Member}'"
            );
            totalFilterValues += valueCount;
        }

        RejectIfGreaterThan(totalFilterValues, MaxTotalFilterValues, "total filter values");
    }

    private static void ValidateTimeDimensions(IReadOnlyList<TimeDimensionQuery>? timeDimensions)
    {
        RejectIfGreaterThan(Count(timeDimensions), MaxTimeDimensions, "time dimensions");
        foreach (var timeDimension in (timeDimensions ?? []).OfType<TimeDimensionQuery>())
        {
            ValidateTextLength(timeDimension.Dimension, "time dimension member");
            ValidateTextLength(timeDimension.Granularity, "time dimension granularity");
        }
    }

    private static void ValidateSorts(IReadOnlyList<SortQuery>? sorts)
    {
        RejectIfGreaterThan(Count(sorts), MaxSorts, "sorts");
        foreach (var sort in (sorts ?? []).OfType<SortQuery>())
        {
            ValidateTextLength(sort.Member, "sort member");
            ValidateTextLength(sort.Direction, "sort direction");
        }
    }

    private static int Count<T>(IReadOnlyCollection<T>? values) => values?.Count ?? 0;

    private static void ValidateMemberNames(IReadOnlyList<string>? names, string description)
    {
        foreach (var name in names ?? [])
        {
            ValidateTextLength(name, description);
        }
    }

    private static void ValidateTextLength(string? value, string description)
    {
        if (value?.Length > MaxIdentifierLength)
        {
            throw new SemanticValidationException(
                $"Query {description} cannot exceed {MaxIdentifierLength} characters."
            );
        }
    }

    private static void RejectIfGreaterThan(long actual, int maximum, string description)
    {
        if (actual > maximum)
        {
            throw new SemanticValidationException(
                $"Query can contain at most {maximum} {description}."
            );
        }
    }
}
