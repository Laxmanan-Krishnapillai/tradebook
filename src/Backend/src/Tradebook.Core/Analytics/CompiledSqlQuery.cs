namespace Tradebook.Core.Analytics;

public sealed class CompiledSqlQuery
{
    public required string SqlText { get; init; }
    public required IReadOnlyDictionary<string, object> Parameters { get; init; }
    public required IReadOnlyList<string> ResultColumnNames { get; init; }
}
