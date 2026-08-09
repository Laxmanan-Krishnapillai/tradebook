namespace Tradebook.Core.Analytics;

public sealed class MeasureDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Sql { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Format { get; set; }
}
