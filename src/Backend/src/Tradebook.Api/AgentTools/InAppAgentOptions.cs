namespace Tradebook.Api.AgentTools;

public sealed class InAppAgentOptions
{
    public const string SectionName = "InAppAgent";

    public bool Enabled { get; init; }
    public string Endpoint { get; init; } = string.Empty;
    public string DeploymentName { get; init; } = string.Empty;
    public string? ManagedIdentityClientId { get; init; }
}
