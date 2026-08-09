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
        DateTime OccurredAt);

    [Fact]
    public async Task Legacy_history_and_writes_are_preserved_during_the_expand_migration()
    {
        var publicEventId = Guid.NewGuid();
        var dashboardEventId = Guid.NewGuid();
        var dashboardId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var publicOccurredAt = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var dashboardOccurredAt = new DateTimeOffset(2030, 2, 3, 4, 5, 6, TimeSpan.Zero);
        const string publicPayload = """{"aggregateId":"legacy-delivery","version":4}""";
        var dashboardPayload = $$"""{"dashboardId":"{{dashboardId}}","actorId":"{{actorId}}","version":9}""";

        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
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
                """,
                new
                {
                    PublicEventId = publicEventId,
                    PublicPayload = publicPayload,
                    PublicOccurredAt = publicOccurredAt,
                    DashboardEventId = dashboardEventId,
                    DashboardId = dashboardId.ToString(),
                    DashboardPayload = dashboardPayload,
                    DashboardOccurredAt = dashboardOccurredAt,
                },
                transaction));

            var migrationSql = await File.ReadAllTextAsync(FindRepositoryFile(
                "src/Database/Migrations/014_wolverine_realtime.sql"));
            await connection.ExecuteAsync(new CommandDefinition(
                migrationSql,
                transaction: transaction));

            var rows = (await connection.QueryAsync<MigratedEvent>(new CommandDefinition(
                """
                SELECT event_id AS "EventId", sequence_id AS "SequenceId",
                       group_name AS "GroupName", occurred_at AS "OccurredAt"
                FROM public.realtime_event_log
                ORDER BY sequence_id
                """,
                transaction: transaction))).ToArray();

            Assert.Collection(
                rows,
                row => Assert.Equal(
                    (publicEventId, 7L, "entity:PhysicalDelivery", publicOccurredAt.UtcDateTime),
                    (row.EventId, row.SequenceId, row.GroupName, row.OccurredAt)),
                row => Assert.Equal(
                    (dashboardEventId, 11L, $"dashboard:{actorId}", dashboardOccurredAt.UtcDateTime),
                    (row.EventId, row.SequenceId, row.GroupName, row.OccurredAt)));
            Assert.True(await PayloadMatchesAsync(
                connection, transaction, publicEventId, publicPayload));
            Assert.True(await PayloadMatchesAsync(
                connection, transaction, dashboardEventId, dashboardPayload));
            Assert.True(await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT to_regclass('public.outbox_events') IS NOT NULL",
                transaction: transaction)));
            Assert.True(await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT to_regprocedure('public.notify_outbox_new_event()') IS NOT NULL",
                transaction: transaction)));

            var compatibilityEventId = Guid.NewGuid();
            var compatibilitySequence = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                INSERT INTO public.outbox_events (
                    event_id, aggregate_type, aggregate_id, event_type, payload)
                VALUES (@EventId, 'Hedge', 'legacy-after-migration', 'Created', '{}'::jsonb)
                RETURNING sequence_id
                """,
                new { EventId = compatibilityEventId },
                transaction));
            Assert.Equal(12, compatibilitySequence);
            Assert.Equal(12, await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                "SELECT sequence_id FROM realtime_event_log WHERE event_id = @EventId",
                new { EventId = compatibilityEventId },
                transaction)));
            Assert.True(await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT processed_at IS NULL FROM outbox_events WHERE event_id = @EventId",
                new { EventId = compatibilityEventId },
                transaction)));
            Assert.True(await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT processed_at IS NULL FROM outbox_events WHERE event_id = @EventId",
                new { EventId = dashboardEventId },
                transaction)));

            var wolverineEventId = Guid.NewGuid();
            var nextWolverineSequence = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                INSERT INTO realtime_event_log (
                    event_id, group_name, aggregate_type, aggregate_id, event_type, payload)
                VALUES (@EventId, 'entity:Hedge', 'Hedge', 'next', 'Created', '{}'::jsonb)
                RETURNING sequence_id
                """,
                new { EventId = wolverineEventId },
                transaction));
            Assert.Equal(13, nextWolverineSequence);
            Assert.True(await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                """
                SELECT sequence_id = 13 AND processed_at IS NOT NULL
                FROM outbox_events
                WHERE event_id = @EventId
                """,
                new { EventId = wolverineEventId },
                transaction)));
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    [Fact]
    public async Task Compatibility_bridge_is_visible_after_commit_and_duplicate_safe()
    {
        var legacyEventId = Guid.NewGuid();
        long legacySequence;
        await using (var legacyConnection = new NpgsqlConnection(Postgres.ConnectionString))
        {
            legacySequence = await legacyConnection.ExecuteScalarAsync<long>(
                """
                INSERT INTO outbox_events (
                    event_id, aggregate_type, aggregate_id, event_type, payload)
                VALUES (@EventId, 'Hedge', 'legacy-committed', 'Created', '{}'::jsonb)
                RETURNING sequence_id
                """,
                new { EventId = legacyEventId });
        }

        var wolverineEventId = Guid.NewGuid();
        long wolverineSequence;
        await using (var wolverineConnection = new NpgsqlConnection(Postgres.ConnectionString))
        {
            wolverineSequence = await wolverineConnection.ExecuteScalarAsync<long>(
                """
                INSERT INTO realtime_event_log (
                    event_id, group_name, aggregate_type, aggregate_id, event_type, payload)
                VALUES (@EventId, 'entity:Hedge', 'Hedge', 'wolverine-committed', 'Created', '{}'::jsonb)
                RETURNING sequence_id
                """,
                new { EventId = wolverineEventId });
            await wolverineConnection.ExecuteAsync(
                """
                INSERT INTO realtime_event_log (
                    event_id, group_name, aggregate_type, aggregate_id, event_type, payload)
                VALUES (@EventId, 'entity:Hedge', 'Hedge', 'wolverine-committed', 'Created', '{}'::jsonb)
                ON CONFLICT (event_id) DO NOTHING
                """,
                new { EventId = wolverineEventId });
        }

        Assert.True(wolverineSequence > legacySequence);
        await using var verification = new NpgsqlConnection(Postgres.ConnectionString);
        Assert.Equal(legacySequence, await verification.ExecuteScalarAsync<long>(
            "SELECT sequence_id FROM realtime_event_log WHERE event_id = @EventId",
            new { EventId = legacyEventId }));
        Assert.True(await verification.ExecuteScalarAsync<bool>(
            "SELECT processed_at IS NULL FROM outbox_events WHERE event_id = @EventId",
            new { EventId = legacyEventId }));
        Assert.True(await verification.ExecuteScalarAsync<bool>(
            """
            SELECT sequence_id = @SequenceId AND processed_at IS NOT NULL
            FROM outbox_events
            WHERE event_id = @EventId
            """,
            new { EventId = wolverineEventId, SequenceId = wolverineSequence }));
        Assert.Equal(1, await verification.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM realtime_event_log WHERE event_id = @EventId",
            new { EventId = wolverineEventId }));
        Assert.Equal(1, await verification.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM outbox_events WHERE event_id = @EventId",
            new { EventId = wolverineEventId }));
    }

    private static Task<bool> PayloadMatchesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid eventId,
        string payload) =>
        connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT payload = CAST(@Payload AS jsonb) FROM realtime_event_log WHERE event_id = @EventId",
            new { Payload = payload, EventId = eventId },
            transaction));

    private static string FindRepositoryFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(
                directory.FullName,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            $"Could not locate repository file '{relativePath}'.",
            relativePath);
    }
}
