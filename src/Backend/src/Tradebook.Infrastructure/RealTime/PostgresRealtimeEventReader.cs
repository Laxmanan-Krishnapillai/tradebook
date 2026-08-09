using System.Data;
using Tradebook.Core.Domain;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;
using Tradebook.Infrastructure.Data;

namespace Tradebook.Infrastructure.RealTime;

public sealed class PostgresRealtimeEventReader(INpgsqlConnectionFactory connections)
    : IRealtimeEventReader
{
    public async Task<GetEventsSinceResponse> GetSinceAsync(
        long afterSequence,
        int limit,
        Guid actorId,
        CancellationToken cancellationToken
    )
    {
        var connection = await connections
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var configuredConnection = connection.ConfigureAwait(false);
        var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            .ConfigureAwait(false);
        await using var configuredTransaction = transaction.ConfigureAwait(false);
        var command = connection.CreateCommand();
        await using var configuredCommand = command.ConfigureAwait(false);
        command.Transaction = transaction;
        command.CommandText = """
            SELECT event_id, sequence_id, aggregate_type, aggregate_id, event_type, payload::text
            FROM realtime_event_log
            WHERE sequence_id > @afterSequence
              AND (aggregate_type <> @workspaceDashboard OR group_name = @dashboardGroup)
            ORDER BY sequence_id ASC
            LIMIT @limit;
            SELECT COALESCE(MAX(sequence_id), 0) FROM realtime_event_log;
            """;
        command.Parameters.AddWithValue("afterSequence", afterSequence);
        command.Parameters.AddWithValue("limit", limit);
        command.Parameters.AddWithValue(
            "workspaceDashboard",
            RealtimeAggregateTypes.WorkspaceDashboard
        );
        command.Parameters.AddWithValue("dashboardGroup", $"dashboard:{actorId}");

        var events = new List<EntityChangedEventDto>();
        var latestSequence = 0L;
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                events.Add(
                    new EntityChangedEventDto(
                        reader.GetGuid(0),
                        reader.GetInt64(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetString(4),
                        reader.GetString(5)
                    )
                );
            }

            if (
                await reader.NextResultAsync(cancellationToken).ConfigureAwait(false)
                && await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            )
            {
                latestSequence = reader.GetInt64(0);
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new GetEventsSinceResponse(events.AsReadOnly(), latestSequence);
    }
}
