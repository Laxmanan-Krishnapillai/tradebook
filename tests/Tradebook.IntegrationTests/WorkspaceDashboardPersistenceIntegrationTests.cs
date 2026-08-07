using Dapper;
using Npgsql;

namespace Tradebook.IntegrationTests;

public sealed class WorkspaceDashboardPersistenceIntegrationTests(PostgresTestFixture postgres) : IClassFixture<PostgresTestFixture>
{
    [Fact]
    public async Task Dashboard_writes_are_audited_outboxed_and_versioned()
    {
        var actorId = Guid.NewGuid();
        var dashboardId = Guid.NewGuid();

        var created = await SaveAsync(dashboardId, actorId, "{\"dashboardId\":\"first\",\"widgets\":[]}", 1);
        Assert.Equal(1, created);

        var updated = await SaveAsync(dashboardId, actorId, "{\"dashboardId\":\"second\",\"widgets\":[]}", created!.Value);
        Assert.Equal(2, updated);

        var stale = await SaveAsync(dashboardId, actorId, "{\"dashboardId\":\"stale\",\"widgets\":[]}", created.Value);
        Assert.Null(stale);

        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        var audit = (await connection.QueryAsync<(string Operation, Guid ActorId)>("""
            SELECT operation AS Operation, actor_id AS ActorId
            FROM audit_log
            WHERE entity_name = 'workspace_dashboards' AND entity_id = @DashboardId
            ORDER BY lower(system_time)
            """, new { DashboardId = dashboardId.ToString() })).ToList();
        Assert.Collection(audit,
            entry => { Assert.Equal("INSERT", entry.Operation); Assert.Equal(actorId, entry.ActorId); },
            entry => { Assert.Equal("UPDATE", entry.Operation); Assert.Equal(actorId, entry.ActorId); });

        var events = (await connection.QueryAsync<(string AggregateType, string EventType)>("""
            SELECT aggregate_type AS AggregateType, event_type AS EventType
            FROM outbox_events
            WHERE aggregate_id = @DashboardId
            ORDER BY sequence_id
            """, new { DashboardId = dashboardId.ToString() })).ToList();
        Assert.Equal([("WorkspaceDashboard", "Created"), ("WorkspaceDashboard", "Updated")], events);
    }

    private async Task<long?> SaveAsync(Guid dashboardId, Guid actorId, string layout, long expectedVersion)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await connection.ExecuteAsync(new CommandDefinition("SELECT set_config('app.actor_id', @ActorId, true)",
            new { ActorId = actorId.ToString() }, transaction));

        const string save = """
            INSERT INTO workspace_dashboards (id, actor_id, layout_json, version)
            VALUES (@DashboardId, @ActorId, CAST(@Layout AS jsonb), 1)
            ON CONFLICT (id) DO UPDATE SET layout_json = EXCLUDED.layout_json,
                version = workspace_dashboards.version + 1, updated_at = clock_timestamp()
            WHERE workspace_dashboards.actor_id = @ActorId AND workspace_dashboards.version = @ExpectedVersion
            RETURNING version, (xmax = 0) AS created;
            """;
        var result = await connection.QuerySingleOrDefaultAsync<(long Version, bool Created)>(new CommandDefinition(save,
            new { DashboardId = dashboardId, ActorId = actorId, Layout = layout, ExpectedVersion = expectedVersion }, transaction));
        if (result == default)
        {
            await transaction.RollbackAsync();
            return null;
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO outbox_events (aggregate_type, aggregate_id, event_type, payload)
            VALUES ('WorkspaceDashboard', @DashboardId::text, @EventType,
                    jsonb_build_object('dashboardId', @DashboardId::text, 'version', @Version));
            """, new { DashboardId = dashboardId, EventType = result.Created ? "Created" : "Updated", result.Version }, transaction));
        await transaction.CommitAsync();
        return result.Version;
    }
}
