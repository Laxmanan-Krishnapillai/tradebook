namespace Tradebook.Core.Analytics;

public sealed class SemanticModelConfig
{
    public string Version { get; set; } = string.Empty;
    public SemanticModelRoot SemanticModel { get; set; } = new();
}

public sealed class SemanticModelRoot
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TargetEntity { get; set; } = string.Empty;
    public List<EntityDefinition> Entities { get; set; } = [];
    public List<JoinDefinition> Joins { get; set; } = [];
    public List<DimensionDefinition> Dimensions { get; set; } = [];
    public List<MeasureDefinition> Measures { get; set; } = [];
    public List<MetricDefinition> Metrics { get; set; } = [];
}

public sealed class EntityDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public string PrimaryKey { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = [];
    public string Description { get; set; } = string.Empty;
}

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

public sealed class DimensionDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Sql { get; set; } = string.Empty;
    public string? JsonbKey { get; set; }
    public string? Description { get; set; }
    public List<string>? Granularity { get; set; }
}

public sealed class MeasureDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Sql { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Format { get; set; }
}

public sealed class MetricDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    public string? Format { get; set; }
}

public sealed record JoinChainStep(JoinDefinition Join, string NewEntity);

public sealed class SemanticSchemaMismatchException(string message) : Exception(message);
