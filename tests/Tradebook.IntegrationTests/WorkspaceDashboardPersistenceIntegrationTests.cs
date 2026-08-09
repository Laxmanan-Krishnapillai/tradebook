using Dapper;
using Npgsql;

namespace Tradebook.IntegrationTests;

public sealed class WorkspaceDashboardPersistenceIntegrationTests(PostgresTestFixture postgres)
    : PostgresDatabaseTestBase(postgres)
{
    [Fact]
    public async Task DashboardWritesAreAuditedOutboxedAndVersioned()
    {
        var actorId = Guid.NewGuid();
        var dashboardId = Guid.NewGuid();

        var created = await SaveAsync(
            dashboardId,
            actorId,
            "{\"dashboardId\":\"first\",\"widgets\":[]}",
            1
        );
        Assert.Equal(1, created);

        var updated = await SaveAsync(
            dashboardId,
            actorId,
            "{\"dashboardId\":\"second\",\"widgets\":[]}",
            created!.Value
        );
        Assert.Equal(2, updated);

        var stale = await SaveAsync(
            dashboardId,
            actorId,
            "{\"dashboardId\":\"stale\",\"widgets\":[]}",
            created.Value
        );
        Assert.Null(stale);

        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await AssertAuditAsync(connection, dashboardId, actorId);
        await AssertOutboxAsync(connection, dashboardId);
    }

    private static async Task AssertAuditAsync(
        NpgsqlConnection connection,
        Guid dashboardId,
        Guid actorId
    )
    {
        var audit = (
            await connection
                .QueryAsync<(string Operation, Guid ActorId)>(
                    """
                    SELECT operation AS Operation, actor_id AS ActorId
                    FROM audit_log
                    WHERE entity_name = 'workspace_dashboards' AND entity_id = @DashboardId
                    ORDER BY lower(system_time)
                    """,
                    new { DashboardId = dashboardId.ToString() }
                )
                .ConfigureAwait(false)
        ).ToList();
        Assert.Collection(
            audit,
            entry =>
            {
                Assert.Equal("INSERT", entry.Operation);
                Assert.Equal(actorId, entry.ActorId);
            },
            entry =>
            {
                Assert.Equal("UPDATE", entry.Operation);
                Assert.Equal(actorId, entry.ActorId);
            }
        );
    }

    private static async Task AssertOutboxAsync(NpgsqlConnection connection, Guid dashboardId)
    {
        var events = (
            await connection
                .QueryAsync<(string AggregateType, string EventType)>(
                    """
                    SELECT aggregate_type AS AggregateType, event_type AS EventType
                    FROM outbox_events
                    WHERE aggregate_id = @DashboardId
                    ORDER BY sequence_id
                    """,
                    new { DashboardId = dashboardId.ToString() }
                )
                .ConfigureAwait(false)
        ).ToList();
        Assert.Equal(
            [("WorkspaceDashboard", "Created"), ("WorkspaceDashboard", "Updated")],
            events
        );
    }

    private async Task<long?> SaveAsync(
        Guid dashboardId,
        Guid actorId,
        string layout,
        long expectedVersion
    )
    {
        var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                await connection
                    .ExecuteAsync(
                        new CommandDefinition(
                            "SELECT set_config('app.actor_id', @ActorId, true)",
                            new { ActorId = actorId.ToString() },
                            transaction
                        )
                    )
                    .ConfigureAwait(false);

                const string save = """
                    INSERT INTO workspace_dashboards (id, actor_id, layout_json, version)
                    VALUES (@DashboardId, @ActorId, CAST(@Layout AS jsonb), 1)
                    ON CONFLICT (id) DO UPDATE SET layout_json = EXCLUDED.layout_json,
                        version = workspace_dashboards.version + 1, updated_at = clock_timestamp()
                    WHERE workspace_dashboards.actor_id = @ActorId AND workspace_dashboards.version = @ExpectedVersion
                    RETURNING version, (xmax = 0) AS created;
                    """;
                var result = await connection
                    .QuerySingleOrDefaultAsync<(long Version, bool Created)>(
                        new CommandDefinition(
                            save,
                            new
                            {
                                DashboardId = dashboardId,
                                ActorId = actorId,
                                Layout = layout,
                                ExpectedVersion = expectedVersion,
                            },
                            transaction
                        )
                    )
                    .ConfigureAwait(false);
                if (result == default)
                {
                    await transaction.RollbackAsync().ConfigureAwait(false);
                    return null;
                }

                await WriteOutboxAsync(connection, transaction, dashboardId, result)
                    .ConfigureAwait(false);
                await transaction.CommitAsync().ConfigureAwait(false);
                return result.Version;
            }
        }
    }

    private static Task<int> WriteOutboxAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid dashboardId,
        (long Version, bool Created) result
    ) =>
        connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO outbox_events (aggregate_type, aggregate_id, event_type, payload)
                VALUES ('WorkspaceDashboard', @DashboardId::text, @EventType,
                        jsonb_build_object('dashboardId', @DashboardId::text, 'version', @Version));
                """,
                new
                {
                    DashboardId = dashboardId,
                    EventType = result.Created ? "Created" : "Updated",
                    result.Version,
                },
                transaction
            )
        );
}
