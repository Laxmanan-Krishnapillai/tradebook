using Dapper;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Infrastructure.Data;

namespace Tradebook.Api.Features.Dashboards;

public sealed class GetDashboardEndpoint(INpgsqlConnectionFactory connections)
    : Endpoint<GetDashboardRequest, SaveDashboardResponse>
{
    public override void Configure()
    {
        Get("/api/v1/dashboards/{dashboardId}");
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(GetDashboardRequest req, CancellationToken ct)
    {
        var actorId = ActorId.From(User);
        var connection = await (connections.OpenConnectionAsync(ct)).ConfigureAwait(false);
        await using var configuredConnection = connection.ConfigureAwait(false);
        var row = await (
            connection.QuerySingleOrDefaultAsync<DashboardRow>(
                new CommandDefinition(
                    "SELECT layout_json::text AS Layout, version AS Version FROM workspace_dashboards WHERE id = @Id AND actor_id = @ActorId",
                    new { Id = req.DashboardId, ActorId = actorId },
                    cancellationToken: ct
                )
            )
        ).ConfigureAwait(false);
        if (row is null)
        {
            await (Send.NotFoundAsync(ct)).ConfigureAwait(false);
            return;
        }
        await (
            Send.ResponseAsync(DashboardMapper.ToResponse(row, req.DashboardId), cancellation: ct)
        ).ConfigureAwait(false);
    }
}
