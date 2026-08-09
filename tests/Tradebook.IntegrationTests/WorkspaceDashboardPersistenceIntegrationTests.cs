using Dapper;
using Npgsql;
using Tradebook.Core.Domain;
using Tradebook.Core.Messaging;

namespace Tradebook.IntegrationTests;

public sealed class WorkspaceDashboardPersistenceIntegrationTests(PostgresTestFixture postgres)
    : PostgresDatabaseTestBase(postgres)
{
    [Fact]
    public async Task DashboardWritesAreAuditedPublishedAndVersioned()
    {
        var actorId = Guid.NewGuid();
        var dashboardId = Guid.NewGuid();
        var publisher = new RecordingTransactionalEventPublisher();

        var created = await SaveAsync(
            dashboardId,
            actorId,
            "{\"dashboardId\":\"first\",\"widgets\":[]}",
            1,
            publisher
        );
        Assert.Equal(1, created);

        var updated = await SaveAsync(
            dashboardId,
            actorId,
            "{\"dashboardId\":\"second\",\"widgets\":[]}",
            created!.Value,
            publisher
        );
        Assert.Equal(2, updated);

        var stale = await SaveAsync(
            dashboardId,
            actorId,
            "{\"dashboardId\":\"stale\",\"widgets\":[]}",
            created.Value,
            publisher
        );
        Assert.Null(stale);

        await VerifyPersistenceAsync(dashboardId, actorId, publisher);
    }

    private async Task VerifyPersistenceAsync(
        Guid dashboardId,
        Guid actorId,
        RecordingTransactionalEventPublisher publisher
    )
    {
        var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await using var configuredConnection = connection.ConfigureAwait(false);
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

        Assert.Equal(
            [
                (RealtimeAggregateTypes.WorkspaceDashboard, "Created"),
                (RealtimeAggregateTypes.WorkspaceDashboard, "Updated"),
            ],
            publisher.Events.Select(item => (item.AggregateType, item.EventType))
        );
        Assert.All(
            publisher.Events,
            item => Assert.Equal(dashboardId.ToString(), item.AggregateId)
        );
        Assert.Equal(2, publisher.Transactions.Count);
        Assert.Equal(2, publisher.FlushCount);
    }

    private async Task<long?> SaveAsync(
        Guid dashboardId,
        Guid actorId,
        string layout,
        long expectedVersion,
        RecordingTransactionalEventPublisher publisher
    )
    {
        var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await using var configuredConnection = connection.ConfigureAwait(false);
        await connection.OpenAsync().ConfigureAwait(false);
        var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);
        await using var configuredTransaction = transaction.ConfigureAwait(false);
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

        await publisher.EnlistAsync(transaction, default).ConfigureAwait(false);
        await publisher
            .PublishAsync(
                EntityChangedDomainEvent.Create(
                    RealtimeAggregateTypes.WorkspaceDashboard,
                    dashboardId.ToString(),
                    result.Created ? "Created" : "Updated",
                    result.Version,
                    actorId: actorId
                )
            )
            .ConfigureAwait(false);
        await transaction.CommitAsync().ConfigureAwait(false);
        await publisher.FlushAsync().ConfigureAwait(false);
        return result.Version;
    }
}
