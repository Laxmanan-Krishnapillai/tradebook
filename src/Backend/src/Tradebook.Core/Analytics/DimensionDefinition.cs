namespace Tradebook.Core.Analytics;

public sealed class DimensionDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Sql { get; set; } = string.Empty;
    public string? JsonbKey { get; set; }
    public string? Description { get; set; }
    public IList<string>? Granularity { get; set; }
}
