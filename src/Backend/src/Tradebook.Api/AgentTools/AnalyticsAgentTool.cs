using Microsoft.Extensions.AI;
using Tradebook.Api.Features.Analytics;
using Tradebook.Api.Security;
using Tradebook.Core.Analytics;

namespace Tradebook.Api.AgentTools;

public sealed class AnalyticsAgentTool(
    IHttpContextAccessor httpContextAccessor,
    IServiceScopeFactory scopeFactory
)
{
    public AIFunction CreateFunction() =>
        AIFunctionFactory.Create(
            (Func<JsonQueryAst, CancellationToken, Task<AnalyticsQueryResponse>>)
                QueryAnalyticsAsync,
            AiCapabilityCatalog.AnalyticsQueryMcpTool,
            "Runs a read-only query against a trusted Tradebook semantic model. Identifiers are allowlisted and filter values are parameterized."
        );

    public async Task<AnalyticsQueryResponse> QueryAnalyticsAsync(
        JsonQueryAst query,
        CancellationToken cancellationToken
    )
    {
        var context =
            httpContextAccessor.HttpContext
            ?? throw new UnauthorizedAccessException("An authenticated request is required.");
        _ = ActorId.From(context.User);

        var scope = scopeFactory.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var runner = scope.ServiceProvider.GetRequiredService<AnalyticsQueryRunner>();
            return await runner.QueryAsync(query, cancellationToken).ConfigureAwait(false);
        }
    }
}
