using System.Diagnostics.CodeAnalysis;

namespace Tradebook.Core.Analytics;

public sealed record JsonQueryAst
{
    public JsonQueryAst() { }

    [SetsRequiredMembers]
    public JsonQueryAst(
        string ModelName,
        IReadOnlyList<string>? Measures,
        IReadOnlyList<string>? Metrics,
        IReadOnlyList<string>? Dimensions,
        IReadOnlyList<TimeDimensionQuery>? TimeDimensions,
        IReadOnlyList<FilterQuery>? Filters,
        IReadOnlyList<SortQuery>? Sorts,
        int? Limit,
        int? Offset
    )
    {
        this.ModelName = ModelName;
        this.Measures = Measures;
        this.Metrics = Metrics;
        this.Dimensions = Dimensions;
        this.TimeDimensions = TimeDimensions;
        this.Filters = Filters;
        this.Sorts = Sorts;
        this.Limit = Limit;
        this.Offset = Offset;
    }

    public required string ModelName { get; init; }

    public IReadOnlyList<string>? Measures { get; init; }

    public IReadOnlyList<string>? Metrics { get; init; }

    public IReadOnlyList<string>? Dimensions { get; init; }

    public IReadOnlyList<TimeDimensionQuery>? TimeDimensions { get; init; }

    public IReadOnlyList<FilterQuery>? Filters { get; init; }

    public IReadOnlyList<SortQuery>? Sorts { get; init; }

    public int? Limit { get; init; }

    public int? Offset { get; init; }
}
