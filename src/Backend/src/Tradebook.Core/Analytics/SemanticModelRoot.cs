namespace Tradebook.Core.Analytics;

public sealed class SemanticModelRoot
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TargetEntity { get; set; } = string.Empty;
    public IList<EntityDefinition> Entities { get; set; } = [];
    public IList<JoinDefinition> Joins { get; set; } = [];
    public IList<DimensionDefinition> Dimensions { get; set; } = [];
    public IList<MeasureDefinition> Measures { get; set; } = [];
    public IList<MetricDefinition> Metrics { get; set; } = [];
}
