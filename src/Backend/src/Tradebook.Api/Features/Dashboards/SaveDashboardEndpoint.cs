using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using FastEndpoints;
using Tradebook.Api.Features.PhysicalDeliveries;
using Tradebook.Core.DTOs;
using Tradebook.Infrastructure.Data;

namespace Tradebook.Api.Features.Dashboards;

public sealed class SaveDashboardEndpoint(INpgsqlConnectionFactory connections) : Endpoint<SaveDashboardRequest, SaveDashboardResponse>
{
    private sealed record DashboardRow(string Layout, long Version);

    public override void Configure() { Put("/api/v1/dashboards/{dashboardId}"); Policies("ReadPolicy"); }

    public override async Task HandleAsync(SaveDashboardRequest request, CancellationToken cancellationToken)
    {
        if (!DashboardLayoutValidator.TryValidate(request.DashboardId, request.Version, request.Layout, out var error))
        {
            AddError(error); await SendErrorsAsync(400, cancellationToken); return;
        }

        var actorId = ActorId.From(User);
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("SELECT set_config('app.actor_id', @ActorId, true)", new { ActorId = actorId.ToString() }, transaction, cancellationToken: cancellationToken));
        var storedLayout = LayoutWithVersion(request.Layout, request.Version + 1).GetRawText();
        var eventType = request.Version == 0 ? "Created" : "Updated";
        DashboardRow? saved = request.Version == 0
            ? await connection.QuerySingleOrDefaultAsync<DashboardRow>(new CommandDefinition("""
                INSERT INTO workspace_dashboards (id, actor_id, layout_json, version)
                VALUES (@Id, @ActorId, @Layout::jsonb, 1)
                ON CONFLICT (id) DO NOTHING
                RETURNING layout_json::text AS Layout, version AS Version;
                """, new { Id = request.DashboardId, ActorId = actorId, Layout = storedLayout }, transaction, cancellationToken: cancellationToken))
            : await connection.QuerySingleOrDefaultAsync<DashboardRow>(new CommandDefinition("""
                UPDATE workspace_dashboards
                SET layout_json = @Layout::jsonb, version = version + 1, updated_at = clock_timestamp()
                WHERE id = @Id AND actor_id = @ActorId AND version = @ExpectedVersion
                RETURNING layout_json::text AS Layout, version AS Version;
                """, new { Id = request.DashboardId, ActorId = actorId, ExpectedVersion = request.Version, Layout = storedLayout }, transaction, cancellationToken: cancellationToken));

        if (saved is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            var current = await CurrentForActorAsync(connection, request.DashboardId, actorId, cancellationToken);
            if (current is null) { await SendNotFoundAsync(cancellationToken); return; }
            await SendAsync(ToResponse(request.DashboardId, current), 409, cancellationToken);
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO outbox_events (aggregate_type, aggregate_id, event_type, payload)
            VALUES ('WorkspaceDashboard', @Id::text, @EventType, jsonb_build_object('dashboardId', @Id::text, 'version', @Version));
            """, new { Id = request.DashboardId, EventType = eventType, saved.Version }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        await SendAsync(ToResponse(request.DashboardId, saved), cancellation: cancellationToken);
    }

    private static Task<DashboardRow?> CurrentForActorAsync(System.Data.IDbConnection connection, Guid id, Guid actorId, CancellationToken cancellationToken) => connection.QuerySingleOrDefaultAsync<DashboardRow>(new CommandDefinition("SELECT layout_json::text AS Layout, version AS Version FROM workspace_dashboards WHERE id = @Id AND actor_id = @ActorId", new { Id = id, ActorId = actorId }, cancellationToken: cancellationToken));
    private static SaveDashboardResponse ToResponse(Guid id, DashboardRow row) => new(id, row.Version, JsonDocument.Parse(row.Layout).RootElement.Clone());
    private static JsonElement LayoutWithVersion(JsonElement layout, long version) { var copy = JsonNode.Parse(layout.GetRawText())!.AsObject(); copy["version"] = version; return JsonSerializer.SerializeToElement(copy); }
}
