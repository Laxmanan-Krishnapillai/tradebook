using System.Text.Json;
using Dapper;
using FastEndpoints;
using Tradebook.Api.Features.PhysicalDeliveries;
using Tradebook.Core.DTOs;
using Tradebook.Infrastructure.Data;

namespace Tradebook.Api.Features.Dashboards;

public sealed class SaveDashboardEndpoint(INpgsqlConnectionFactory connections) : Endpoint<SaveDashboardRequest, SaveDashboardResponse>
{
    private static readonly HashSet<string> ChartTypes = ["KPI_CARD", "SPARK_LINE", "LINE", "AREA", "BAR", "STACKED_BAR", "SCATTER", "HEATMAP", "CANDLESTICK", "TABLE"];
    private sealed record DashboardWriteResult(long Version, bool Created);
    public override void Configure() { Put("/api/v1/dashboards/{dashboardId}"); Policies("ReadPolicy"); }
    public override async Task HandleAsync(SaveDashboardRequest request, CancellationToken cancellationToken)
    {
        if (!IsValidLayout(request.DashboardId, request.Layout, out var error)) { AddError(error); await SendErrorsAsync(400, cancellationToken); return; }
        var actorId = ActorId.From(User);
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "SELECT set_config('app.actor_id', @ActorId, true)", new { ActorId = actorId.ToString() }, transaction,
            cancellationToken: cancellationToken));
        const string sql = """
            INSERT INTO workspace_dashboards (id, actor_id, layout_json, version)
            VALUES (@Id, @ActorId, CAST(@Layout AS jsonb), 1)
            ON CONFLICT (id) DO UPDATE SET layout_json = EXCLUDED.layout_json, version = workspace_dashboards.version + 1, updated_at = now()
            WHERE workspace_dashboards.actor_id = @ActorId AND workspace_dashboards.version = @Version
            RETURNING version, (xmax = 0) AS created;
            """;
        var write = await connection.QuerySingleOrDefaultAsync<DashboardWriteResult>(new CommandDefinition(sql,
            new { Id = request.DashboardId, ActorId = actorId, Layout = request.Layout.GetRawText(), request.Version }, transaction,
            cancellationToken: cancellationToken));
        if (write is null) { await transaction.RollbackAsync(cancellationToken); await SendAsync(new SaveDashboardResponse(request.DashboardId, request.Version, request.Layout), 409, cancellationToken); return; }
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO outbox_events (aggregate_type, aggregate_id, event_type, payload)
            VALUES ('WorkspaceDashboard', @Id::text, @EventType,
                    jsonb_build_object('dashboardId', @Id::text, 'version', @Version));
            """, new { Id = request.DashboardId, EventType = write.Created ? "Created" : "Updated", write.Version }, transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        await SendAsync(new SaveDashboardResponse(request.DashboardId, write.Version, request.Layout), 200, cancellationToken);
    }
    private static bool IsValidLayout(Guid id, JsonElement layout, out string error)
    {
        error = "Dashboard layout must match dashboardSchema.json.";
        if (id == Guid.Empty || layout.ValueKind != JsonValueKind.Object || !layout.TryGetProperty("dashboardId", out var layoutId) || layoutId.GetString() != id.ToString() || !layout.TryGetProperty("widgets", out var widgets) || widgets.ValueKind != JsonValueKind.Array) return false;
        foreach (var widget in widgets.EnumerateArray()) if (widget.ValueKind != JsonValueKind.Object || !widget.TryGetProperty("chartType", out var chartType) || !ChartTypes.Contains(chartType.GetString() ?? "")) return false;
        return true;
    }
}
