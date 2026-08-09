namespace Tradebook.Core.Analytics;

public sealed class SemanticModelConfig
{
    public string Version { get; set; } = string.Empty;
    public SemanticModelRoot SemanticModel { get; set; } = new();
}
