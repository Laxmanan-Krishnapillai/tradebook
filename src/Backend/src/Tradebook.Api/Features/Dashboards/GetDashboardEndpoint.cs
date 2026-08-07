using System.Text.Json;
using Dapper;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Infrastructure.Data;

namespace Tradebook.Api.Features.Dashboards;

public sealed class GetDashboardRequest { public Guid DashboardId { get; init; } }

public sealed class GetDashboardEndpoint(INpgsqlConnectionFactory connections) : Endpoint<GetDashboardRequest, SaveDashboardResponse>
{
    private sealed record DashboardRow(string Layout, long Version);
    public override void Configure() { Get("/api/v1/dashboards/{dashboardId}"); Policies("ReadPolicy"); }
    public override async Task HandleAsync(GetDashboardRequest request, CancellationToken cancellationToken)
    {
        var actorId = ActorId.From(User);
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<DashboardRow>(new CommandDefinition("SELECT layout_json::text AS Layout, version AS Version FROM workspace_dashboards WHERE id = @Id AND actor_id = @ActorId", new { Id = request.DashboardId, ActorId = actorId }, cancellationToken: cancellationToken));
        if (row is null) { await SendNotFoundAsync(cancellationToken); return; }
        await SendAsync(new SaveDashboardResponse(request.DashboardId, row.Version, JsonDocument.Parse(row.Layout).RootElement.Clone()), cancellation: cancellationToken);
    }
}
