using System.ComponentModel;
using ModelContextProtocol.Server;
using Tradebook.Api.Features.Analytics;
using Tradebook.Core.Analytics;

namespace Tradebook.Api.AgentTools;

[McpServerToolType]
public sealed class AnalyticsMcpTools(AnalyticsQueryRunner queries)
{
    [McpServerTool(
        Name = AiCapabilityCatalog.AnalyticsQueryMcpTool,
        Title = "Query Tradebook analytics",
        ReadOnly = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true
    )]
    [Description(
        "Runs a read-only query against a trusted Tradebook semantic model. Identifiers are allowlisted and filter values are parameterized."
    )]
    public Task<AnalyticsQueryResponse> QueryAnalyticsAsync(
        [Description("The semantic query AST to validate and execute.")] JsonQueryAst query,
        CancellationToken cancellationToken
    ) => queries.QueryAsync(query, cancellationToken);
}
