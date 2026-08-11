namespace Tradebook.Api.AgentTools;

/// <summary>
/// Declares the user-visible capabilities exposed through both REST and AI transports.
/// </summary>
public static class AiCapabilityCatalog
{
    public const string AnalyticsQueryName = "analytics.query";
    public const string AnalyticsQueryRestRoute = "/api/v1/analytics/query";
    public const string AnalyticsQueryMcpTool = "tradebook_query_analytics";

    public static IReadOnlyList<AiCapabilityDescriptor> All { get; } =
    [new(AnalyticsQueryName, AnalyticsQueryRestRoute, AnalyticsQueryMcpTool, IsReadOnly: true)];
}
