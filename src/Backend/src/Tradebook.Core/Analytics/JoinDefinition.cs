namespace Tradebook.Core.Analytics;

public sealed class JoinDefinition
{
    public string Name { get; set; } = string.Empty;
    public string LeftEntity { get; set; } = string.Empty;
    public string RightEntity { get; set; } = string.Empty;
    public string JoinType { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public string LeftColumn { get; set; } = string.Empty;
    public string RightColumn { get; set; } = string.Empty;
}
