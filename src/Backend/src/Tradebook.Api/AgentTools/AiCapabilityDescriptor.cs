namespace Tradebook.Api.AgentTools;

public sealed record AiCapabilityDescriptor(
    string Name,
    string RestRoute,
    string McpToolName,
    bool IsReadOnly
);
