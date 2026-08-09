using System.Data;
using Tradebook.Core.Domain;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;
using Tradebook.Infrastructure.Data;

namespace Tradebook.Infrastructure.RealTime;

public sealed class PostgresRealtimeEventReader(INpgsqlConnectionFactory connections) : IRealtimeEventReader
{
    public async Task<GetEventsSinceResponse> GetSinceAsync(
        long afterSequence,
        int limit,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);
        await using var command = connection.CreateCommand();
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
        command.Parameters.AddWithValue("workspaceDashboard", RealtimeAggregateTypes.WorkspaceDashboard);
        command.Parameters.AddWithValue("dashboardGroup", $"dashboard:{actorId}");

        var events = new List<EntityChangedEventDto>();
        var latestSequence = 0L;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                events.Add(new EntityChangedEventDto(
                    reader.GetGuid(0),
                    reader.GetInt64(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5)));
            }

            if (await reader.NextResultAsync(cancellationToken) &&
                await reader.ReadAsync(cancellationToken))
            {
                latestSequence = reader.GetInt64(0);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return new GetEventsSinceResponse(events.AsReadOnly(), latestSequence);
    }
}
