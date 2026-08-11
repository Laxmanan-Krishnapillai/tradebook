using FastEndpoints;
using Microsoft.Extensions.Options;
using Tradebook.Api.AgentTools;

namespace Tradebook.Api.Features.Agent;

public sealed class GetInAppAgentStatusEndpoint(IOptions<InAppAgentOptions> configured)
    : EndpointWithoutRequest<InAppAgentStatusResponse>
{
    public override void Configure()
    {
        Get("/api/v1/agent/status");
        Policies("ReadPolicy");
    }

    public override Task<InAppAgentStatusResponse> ExecuteAsync(CancellationToken ct) =>
        Task.FromResult(
            new InAppAgentStatusResponse(
                configured.Value.Enabled,
                ReadOnly: true,
                Transport: "AG-UI",
                RunPath: "/api/v1/agent/run"
            )
        );
}
