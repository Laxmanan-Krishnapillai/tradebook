namespace Tradebook.Core.Analytics;

public sealed class MetricDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    public string? Format { get; set; }
}
