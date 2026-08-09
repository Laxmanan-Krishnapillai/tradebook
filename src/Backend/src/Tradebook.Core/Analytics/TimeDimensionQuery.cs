using System.Diagnostics.CodeAnalysis;

namespace Tradebook.Core.Analytics;

public sealed record TimeDimensionQuery
{
    public TimeDimensionQuery() { }

    [SetsRequiredMembers]
    public TimeDimensionQuery(string Dimension, string Granularity, string[]? DateRange)
    {
        this.Dimension = Dimension;
        this.Granularity = Granularity;
        this.DateRange = DateRange;
    }

    public required string Dimension { get; init; }

    public required string Granularity { get; init; }

    public string[]? DateRange { get; init; }
}
