namespace Tradebook.Core.Analytics;

public sealed class EntityDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public string PrimaryKey { get; set; } = string.Empty;
    public IList<string> Columns { get; set; } = [];
    public string Description { get; set; } = string.Empty;
}
