using FastEndpoints;
using Tradebook.Api.AgentTools;
using Tradebook.Core.Analytics;

namespace Tradebook.Api.Features.Analytics;

public sealed class AnalyticsQueryEndpoint(AnalyticsQueryRunner queries)
    : Endpoint<JsonQueryAst, AnalyticsQueryResponse>
{
    public override void Configure()
    {
        Post(AiCapabilityCatalog.AnalyticsQueryRestRoute);
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(JsonQueryAst req, CancellationToken ct)
    {
        try
        {
            var response = await queries.QueryAsync(req, ct).ConfigureAwait(false);
            await (Send.ResponseAsync(response, 200, cancellation: ct)).ConfigureAwait(false);
        }
        catch (SemanticValidationException exception)
        {
            AddError(exception.Message);
            await (Send.ErrorsAsync(400, cancellation: ct)).ConfigureAwait(false);
        }
    }
}
