namespace Tradebook.Core.Analytics;

internal readonly record struct ParsedTemporalValue(
    object DatabaseValue,
    DateTimeOffset Comparable
);
