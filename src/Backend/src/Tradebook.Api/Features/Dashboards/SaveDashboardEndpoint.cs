using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using FastEndpoints;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tradebook.Api.Security;
using Tradebook.Core.Analytics;
using Tradebook.Core.DTOs;
using Tradebook.Infrastructure.Data;

namespace Tradebook.Api.Features.Dashboards;

public sealed class SaveDashboardEndpoint(
    INpgsqlConnectionFactory connections,
    SemanticQueryCompiler semanticQueries,
    IOptions<JsonOptions> jsonOptions
) : Endpoint<SaveDashboardRequest, SaveDashboardResponse>
{
    private const string InsertSql = """
        INSERT INTO workspace_dashboards (id, actor_id, layout_json, version)
        VALUES (@Id, @ActorId, @Layout::jsonb, 1)
        ON CONFLICT (id) DO NOTHING
        RETURNING layout_json::text AS Layout, version AS Version;
        """;

    private const string UpdateSql = """
        UPDATE workspace_dashboards
        SET layout_json = @Layout::jsonb, version = version + 1, updated_at = clock_timestamp()
        WHERE id = @Id AND actor_id = @ActorId AND version = @ExpectedVersion
        RETURNING layout_json::text AS Layout, version AS Version;
        """;

    private const string OutboxSql = """
        INSERT INTO outbox_events (aggregate_type, aggregate_id, event_type, payload)
        VALUES ('WorkspaceDashboard', @Id::text, @EventType,
                jsonb_build_object('dashboardId', @Id::text, 'actorId', @ActorId::text, 'version', @Version));
        """;

    public override void Configure()
    {
        Put("/api/v1/dashboards/{dashboardId}");
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(SaveDashboardRequest req, CancellationToken ct)
    {
        if (!TryValidate(req, out var error))
        {
            AddError(error);
            await (Send.ErrorsAsync(400, cancellation: ct)).ConfigureAwait(false);
            return;
        }

        var actorId = ActorId.From(User);
        var connection = await (connections.OpenConnectionAsync(ct)).ConfigureAwait(false);
        await using var configuredConnection = connection.ConfigureAwait(false);
        var transaction = await (connection.BeginTransactionAsync(ct)).ConfigureAwait(false);
        await using var configuredTransaction = transaction.ConfigureAwait(false);
        await (SetActorAsync(connection, transaction, actorId, ct)).ConfigureAwait(false);
        var storedLayout = LayoutWithVersion(req.Layout, req.Version + 1).GetRawText();
        var eventType = req.Version == 0 ? "Created" : "Updated";
        var saved = await (
            SaveAsync(connection, transaction, req, actorId, storedLayout, ct)
        ).ConfigureAwait(false);

        if (saved is null)
        {
            await (transaction.RollbackAsync(ct)).ConfigureAwait(false);
            await (SendConflictOrNotFoundAsync(connection, req, actorId, ct)).ConfigureAwait(false);
            return;
        }

        await (
            WriteOutboxAsync(
                connection,
                transaction,
                req.DashboardId,
                actorId,
                eventType,
                saved,
                ct
            )
        ).ConfigureAwait(false);
        await (transaction.CommitAsync(ct)).ConfigureAwait(false);
        await (
            Send.ResponseAsync(DashboardMapper.ToResponse(saved, req.DashboardId), cancellation: ct)
        ).ConfigureAwait(false);
    }

    private bool TryValidate(SaveDashboardRequest request, out string error) =>
        DashboardLayoutValidator.TryValidate(
            request.DashboardId,
            request.Version,
            request.Layout,
            semanticQueries,
            jsonOptions.Value.SerializerOptions,
            out error
        );

    private static Task<int> SetActorAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid actorId,
        CancellationToken ct
    ) =>
        connection.ExecuteAsync(
            new CommandDefinition(
                "SELECT set_config('app.actor_id', @ActorId, true)",
                new { ActorId = actorId.ToString() },
                transaction,
                cancellationToken: ct
            )
        );

    private static Task<DashboardRow?> SaveAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        SaveDashboardRequest request,
        Guid actorId,
        string storedLayout,
        CancellationToken ct
    )
    {
        var command =
            request.Version == 0
                ? new CommandDefinition(
                    InsertSql,
                    new
                    {
                        Id = request.DashboardId,
                        ActorId = actorId,
                        Layout = storedLayout,
                    },
                    transaction,
                    cancellationToken: ct
                )
                : new CommandDefinition(
                    UpdateSql,
                    new
                    {
                        Id = request.DashboardId,
                        ActorId = actorId,
                        ExpectedVersion = request.Version,
                        Layout = storedLayout,
                    },
                    transaction,
                    cancellationToken: ct
                );
        return connection.QuerySingleOrDefaultAsync<DashboardRow>(command);
    }

    private async Task SendConflictOrNotFoundAsync(
        IDbConnection connection,
        SaveDashboardRequest request,
        Guid actorId,
        CancellationToken ct
    )
    {
        var current = await (
            CurrentForActorAsync(connection, request.DashboardId, actorId, ct)
        ).ConfigureAwait(false);
        if (current is null)
        {
            await (Send.NotFoundAsync(ct)).ConfigureAwait(false);
            return;
        }
        await (
            Send.ResponseAsync(
                DashboardMapper.ToResponse(current, request.DashboardId),
                409,
                cancellation: ct
            )
        ).ConfigureAwait(false);
    }

    private static Task<int> WriteOutboxAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid dashboardId,
        Guid actorId,
        string eventType,
        DashboardRow saved,
        CancellationToken ct
    ) =>
        connection.ExecuteAsync(
            new CommandDefinition(
                OutboxSql,
                new
                {
                    Id = dashboardId,
                    ActorId = actorId,
                    EventType = eventType,
                    saved.Version,
                },
                transaction,
                cancellationToken: ct
            )
        );

    private static Task<DashboardRow?> CurrentForActorAsync(
        IDbConnection connection,
        Guid id,
        Guid actorId,
        CancellationToken ct
    ) =>
        connection.QuerySingleOrDefaultAsync<DashboardRow>(
            new CommandDefinition(
                "SELECT layout_json::text AS Layout, version AS Version FROM workspace_dashboards WHERE id = @Id AND actor_id = @ActorId",
                new { Id = id, ActorId = actorId },
                cancellationToken: ct
            )
        );

    private static JsonElement LayoutWithVersion(JsonElement layout, long version)
    {
        var copy = JsonNode.Parse(layout.GetRawText())!.AsObject();
        copy["version"] = version;
        return JsonSerializer.SerializeToElement(copy);
    }
}
