using Dapper;
using Npgsql;
using Tradebook.IntegrationTests.Fixtures;

namespace Tradebook.IntegrationTests.RealTime;

[Trait("Category", "RealTime")]
public sealed class RealtimeMigrationTests(PostgresTestFixture postgres)
    : PostgresDatabaseTestBase(postgres)
{
    private sealed record MigratedEvent(
        Guid EventId,
        long SequenceId,
        string GroupName,
        DateTime OccurredAt
    );

    private sealed record LegacyMigrationScenario(
        Guid PublicEventId,
        Guid DashboardEventId,
        Guid DashboardId,
        Guid ActorId,
        DateTimeOffset PublicOccurredAt,
        DateTimeOffset DashboardOccurredAt,
        string PublicPayload,
        string DashboardPayload
    );

    private const string LegacySchemaSql = """
        DROP TABLE public.realtime_event_log;
        DROP TABLE public.outbox_events;
        DROP FUNCTION IF EXISTS public.mirror_realtime_event_to_legacy_outbox();
        DROP FUNCTION IF EXISTS public.notify_outbox_new_event();
        CREATE TABLE public.outbox_events (
            event_id UUID PRIMARY KEY,
            sequence_id BIGSERIAL NOT NULL UNIQUE,
            aggregate_type VARCHAR(128) NOT NULL,
            aggregate_id VARCHAR(128) NOT NULL,
            event_type VARCHAR(128) NOT NULL,
            payload JSONB NOT NULL,
            created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
            processed_at TIMESTAMPTZ
        );
        CREATE OR REPLACE FUNCTION public.notify_outbox_new_event() RETURNS trigger AS $$
        BEGIN
            PERFORM pg_notify('outbox_new_event', NEW.event_id::text);
            RETURN NEW;
        END;
        $$ LANGUAGE plpgsql;
        CREATE TRIGGER trg_outbox_notify AFTER INSERT ON public.outbox_events
        FOR EACH ROW EXECUTE FUNCTION public.notify_outbox_new_event();
        INSERT INTO public.outbox_events (
            event_id, sequence_id, aggregate_type, aggregate_id, event_type,
            payload, created_at, processed_at)
        VALUES
            (@PublicEventId, 7, 'PhysicalDelivery', 'legacy-delivery', 'Updated',
             CAST(@PublicPayload AS jsonb), @PublicOccurredAt, @PublicOccurredAt),
            (@DashboardEventId, 11, 'WorkspaceDashboard', @DashboardId, 'Updated',
             CAST(@DashboardPayload AS jsonb), @DashboardOccurredAt, NULL);
        """;

    [Fact]
    public async Task LegacyHistoryAndWritesArePreservedDuringTheExpandMigration()
    {
        var publicEventId = Guid.NewGuid();
        var dashboardEventId = Guid.NewGuid();
        var dashboardId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var publicOccurredAt = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var dashboardOccurredAt = new DateTimeOffset(2030, 2, 3, 4, 5, 6, TimeSpan.Zero);
        const string publicPayload = """{"aggregateId":"legacy-delivery","version":4}""";
        var dashboardPayload =
            $$"""{"dashboardId":"{{dashboardId}}","actorId":"{{actorId}}","version":9}""";
        var scenario = new LegacyMigrationScenario(
            publicEventId,
            dashboardEventId,
            dashboardId,
            actorId,
            publicOccurredAt,
            dashboardOccurredAt,
            publicPayload,
            dashboardPayload
        );

        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await SeedLegacySchemaAsync(connection, transaction, scenario);

            await ApplyMigrationAsync(connection, transaction);

            await VerifyMigratedHistoryAsync(connection, transaction, scenario);

            await VerifyLegacyWriteAfterMigrationAsync(
                connection,
                transaction,
                scenario.DashboardEventId
            );
            await VerifyWolverineWriteAfterMigrationAsync(connection, transaction);
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    private static Task<int> SeedLegacySchemaAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        LegacyMigrationScenario scenario
    ) =>
        connection.ExecuteAsync(
            new CommandDefinition(
                LegacySchemaSql,
                new
                {
                    scenario.PublicEventId,
                    scenario.PublicPayload,
                    scenario.PublicOccurredAt,
                    scenario.DashboardEventId,
                    DashboardId = scenario.DashboardId.ToString(),
                    scenario.DashboardPayload,
                    scenario.DashboardOccurredAt,
                },
                transaction
            )
        );

    private static async Task ApplyMigrationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction
    )
    {
        var migrationSql = await File.ReadAllTextAsync(
                FindRepositoryFile("src/Database/Migrations/014_wolverine_realtime.sql")
            )
            .ConfigureAwait(false);
        await connection
            .ExecuteAsync(new CommandDefinition(migrationSql, transaction: transaction))
            .ConfigureAwait(false);
    }

    private static async Task VerifyMigratedHistoryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        LegacyMigrationScenario scenario
    )
    {
        await VerifyMigratedRowsAsync(connection, transaction, scenario).ConfigureAwait(false);
        await VerifyLegacyArtifactsAsync(connection, transaction, scenario).ConfigureAwait(false);
    }

    private static async Task VerifyMigratedRowsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        LegacyMigrationScenario scenario
    )
    {
        var rows = (
            await connection
                .QueryAsync<MigratedEvent>(
                    new CommandDefinition(
                        """
                        SELECT event_id AS "EventId", sequence_id AS "SequenceId",
                               group_name AS "GroupName", occurred_at AS "OccurredAt"
                        FROM public.realtime_event_log
                        ORDER BY sequence_id
                        """,
                        transaction: transaction
                    )
                )
                .ConfigureAwait(false)
        ).ToArray();
        Assert.Collection(
            rows,
            row =>
                Assert.Equal(
                    (
                        scenario.PublicEventId,
                        7L,
                        "entity:PhysicalDelivery",
                        scenario.PublicOccurredAt.UtcDateTime
                    ),
                    (row.EventId, row.SequenceId, row.GroupName, row.OccurredAt)
                ),
            row =>
                Assert.Equal(
                    (
                        scenario.DashboardEventId,
                        11L,
                        $"dashboard:{scenario.ActorId}",
                        scenario.DashboardOccurredAt.UtcDateTime
                    ),
                    (row.EventId, row.SequenceId, row.GroupName, row.OccurredAt)
                )
        );
    }

    private static async Task VerifyLegacyArtifactsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        LegacyMigrationScenario scenario
    )
    {
        Assert.True(
            await PayloadMatchesAsync(
                    connection,
                    transaction,
                    scenario.PublicEventId,
                    scenario.PublicPayload
                )
                .ConfigureAwait(false)
        );
        Assert.True(
            await PayloadMatchesAsync(
                    connection,
                    transaction,
                    scenario.DashboardEventId,
                    scenario.DashboardPayload
                )
                .ConfigureAwait(false)
        );
        Assert.True(
            await connection
                .ExecuteScalarAsync<bool>(
                    new CommandDefinition(
                        "SELECT to_regclass('public.outbox_events') IS NOT NULL",
                        transaction: transaction
                    )
                )
                .ConfigureAwait(false)
        );
        Assert.True(
            await connection
                .ExecuteScalarAsync<bool>(
                    new CommandDefinition(
                        "SELECT to_regprocedure('public.notify_outbox_new_event()') IS NOT NULL",
                        transaction: transaction
                    )
                )
                .ConfigureAwait(false)
        );
    }

    private static async Task VerifyLegacyWriteAfterMigrationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid dashboardEventId
    )
    {
        var compatibilityEventId = Guid.NewGuid();
        var compatibilitySequence = await connection
            .ExecuteScalarAsync<long>(
                new CommandDefinition(
                    """
                    INSERT INTO public.outbox_events (
                        event_id, aggregate_type, aggregate_id, event_type, payload)
                    VALUES (@EventId, 'Hedge', 'legacy-after-migration', 'Created', '{}'::jsonb)
                    RETURNING sequence_id
                    """,
                    new { EventId = compatibilityEventId },
                    transaction
                )
            )
            .ConfigureAwait(false);
        Assert.Equal(12, compatibilitySequence);
        Assert.Equal(
            12,
            await connection
                .ExecuteScalarAsync<long>(
                    new CommandDefinition(
                        "SELECT sequence_id FROM realtime_event_log WHERE event_id = @EventId",
                        new { EventId = compatibilityEventId },
                        transaction
                    )
                )
                .ConfigureAwait(false)
        );
        Assert.True(
            await IsLegacyEventPendingAsync(connection, transaction, compatibilityEventId)
                .ConfigureAwait(false)
        );
        Assert.True(
            await IsLegacyEventPendingAsync(connection, transaction, dashboardEventId)
                .ConfigureAwait(false)
        );
    }

    private static async Task VerifyWolverineWriteAfterMigrationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction
    )
    {
        var eventId = Guid.NewGuid();
        var sequence = await connection
            .ExecuteScalarAsync<long>(
                new CommandDefinition(
                    """
                    INSERT INTO realtime_event_log (
                        event_id, group_name, aggregate_type, aggregate_id, event_type, payload)
                    VALUES (@EventId, 'entity:Hedge', 'Hedge', 'next', 'Created', '{}'::jsonb)
                    RETURNING sequence_id
                    """,
                    new { EventId = eventId },
                    transaction
                )
            )
            .ConfigureAwait(false);
        Assert.Equal(13, sequence);
        Assert.True(
            await connection
                .ExecuteScalarAsync<bool>(
                    new CommandDefinition(
                        """
                        SELECT sequence_id = 13 AND processed_at IS NOT NULL
                        FROM outbox_events
                        WHERE event_id = @EventId
                        """,
                        new { EventId = eventId },
                        transaction
                    )
                )
                .ConfigureAwait(false)
        );
    }

    private static Task<bool> IsLegacyEventPendingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid eventId
    ) =>
        connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT processed_at IS NULL FROM outbox_events WHERE event_id = @EventId",
                new { EventId = eventId },
                transaction
            )
        );

    [Fact]
    public async Task CompatibilityBridgeIsVisibleAfterCommitAndDuplicateSafe()
    {
        var legacyEventId = Guid.NewGuid();
        var legacySequence = await InsertCommittedLegacyEventAsync(legacyEventId);

        var wolverineEventId = Guid.NewGuid();
        var wolverineSequence = await InsertCommittedWolverineEventAsync(wolverineEventId);

        Assert.True(wolverineSequence > legacySequence);
        await VerifyCommittedBridgeAsync(
            legacyEventId,
            legacySequence,
            wolverineEventId,
            wolverineSequence
        );
    }

    private async Task<long> InsertCommittedLegacyEventAsync(Guid eventId)
    {
        var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await using var configuredConnection = connection.ConfigureAwait(false);
        return await connection
            .ExecuteScalarAsync<long>(
                """
                INSERT INTO outbox_events (
                    event_id, aggregate_type, aggregate_id, event_type, payload)
                VALUES (@EventId, 'Hedge', 'legacy-committed', 'Created', '{}'::jsonb)
                RETURNING sequence_id
                """,
                new { EventId = eventId }
            )
            .ConfigureAwait(false);
    }

    private async Task<long> InsertCommittedWolverineEventAsync(Guid eventId)
    {
        var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await using var configuredConnection = connection.ConfigureAwait(false);
        var sequence = await connection
            .ExecuteScalarAsync<long>(
                """
                INSERT INTO realtime_event_log (
                    event_id, group_name, aggregate_type, aggregate_id, event_type, payload)
                VALUES (@EventId, 'entity:Hedge', 'Hedge', 'wolverine-committed', 'Created', '{}'::jsonb)
                RETURNING sequence_id
                """,
                new { EventId = eventId }
            )
            .ConfigureAwait(false);
        await connection
            .ExecuteAsync(
                """
                INSERT INTO realtime_event_log (
                    event_id, group_name, aggregate_type, aggregate_id, event_type, payload)
                VALUES (@EventId, 'entity:Hedge', 'Hedge', 'wolverine-committed', 'Created', '{}'::jsonb)
                ON CONFLICT (event_id) DO NOTHING
                """,
                new { EventId = eventId }
            )
            .ConfigureAwait(false);
        return sequence;
    }

    private async Task VerifyCommittedBridgeAsync(
        Guid legacyEventId,
        long legacySequence,
        Guid wolverineEventId,
        long wolverineSequence
    )
    {
        var verification = new NpgsqlConnection(Postgres.ConnectionString);
        await using var configuredVerification = verification.ConfigureAwait(false);
        Assert.Equal(
            legacySequence,
            await verification
                .ExecuteScalarAsync<long>(
                    "SELECT sequence_id FROM realtime_event_log WHERE event_id = @EventId",
                    new { EventId = legacyEventId }
                )
                .ConfigureAwait(false)
        );
        Assert.True(
            await verification
                .ExecuteScalarAsync<bool>(
                    "SELECT processed_at IS NULL FROM outbox_events WHERE event_id = @EventId",
                    new { EventId = legacyEventId }
                )
                .ConfigureAwait(false)
        );
        Assert.True(
            await verification
                .ExecuteScalarAsync<bool>(
                    """
                    SELECT sequence_id = @SequenceId AND processed_at IS NOT NULL
                    FROM outbox_events
                    WHERE event_id = @EventId
                    """,
                    new { EventId = wolverineEventId, SequenceId = wolverineSequence }
                )
                .ConfigureAwait(false)
        );
        Assert.Equal(
            1,
            await verification
                .ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM realtime_event_log WHERE event_id = @EventId",
                    new { EventId = wolverineEventId }
                )
                .ConfigureAwait(false)
        );
        Assert.Equal(
            1,
            await verification
                .ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM outbox_events WHERE event_id = @EventId",
                    new { EventId = wolverineEventId }
                )
                .ConfigureAwait(false)
        );
    }

    private static Task<bool> PayloadMatchesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid eventId,
        string payload
    ) =>
        connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT payload = CAST(@Payload AS jsonb) FROM realtime_event_log WHERE event_id = @EventId",
                new { Payload = payload, EventId = eventId },
                transaction
            )
        );

    private static string FindRepositoryFile(string relativePath)
    {
        for (
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null;
            directory = directory.Parent
        )
        {
            var candidate = Path.Combine(
                directory.FullName,
                relativePath.Replace('/', Path.DirectorySeparatorChar)
            );
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            $"Could not locate repository file '{relativePath}'.",
            relativePath
        );
    }
}
