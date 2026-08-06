using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Tradebook.Core.Interfaces;
using Tradebook.Infrastructure.Data;

namespace Tradebook.Infrastructure.Outbox;

/// <summary>
/// Task 03 outbox dispatcher: LISTEN outbox_new_event wake-up with a fallback poll,
/// transactional batch claiming via FOR UPDATE SKIP LOCKED ordered by sequence_id,
/// fan-out through <see cref="IOutboxEventFanout"/>, then mark-processed in the same
/// transaction. At-least-once: a failed batch is never marked processed.
/// </summary>
public sealed class OutboxDispatcher(
    INpgsqlConnectionFactory connections,
    IOutboxEventFanout fanout,
    IOptions<OutboxOptions> options,
    ILogger<OutboxDispatcher> logger,
    IOutboxDispatchObserver? observer = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var listenConnection = await connections.OpenConnectionAsync(stoppingToken);
                await using (var listen = new NpgsqlCommand("LISTEN outbox_new_event", listenConnection))
                {
                    await listen.ExecuteNonQueryAsync(stoppingToken);
                }

                while (!stoppingToken.IsCancellationRequested)
                {
                    while (await DispatchBatchAsync(stoppingToken) > 0)
                    {
                    }

                    await listenConnection.WaitAsync(TimeSpan.FromSeconds(options.Value.FallbackPollSeconds), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox dispatch failed; backing off {BackoffSeconds}s before retrying.",
                    options.Value.ErrorBackoffSeconds);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(options.Value.ErrorBackoffSeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var batch = new List<OutboxEventRecord>();
        await using (var claim = new NpgsqlCommand("""
            SELECT event_id, sequence_id, aggregate_type, aggregate_id::uuid, event_type, payload::text
            FROM outbox_events
            WHERE processed_at IS NULL
            ORDER BY sequence_id
            LIMIT @batchSize
            FOR UPDATE SKIP LOCKED
            """, connection, transaction))
        {
            claim.Parameters.AddWithValue("batchSize", options.Value.BatchSize);
            await using var reader = await claim.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                batch.Add(new OutboxEventRecord(reader.GetGuid(0), reader.GetInt64(1), reader.GetString(2),
                    reader.GetGuid(3), reader.GetString(4), reader.GetString(5)));
            }
        }

        if (batch.Count == 0)
        {
            return 0;
        }

        foreach (var record in batch)
        {
            await fanout.PublishEntityChangedAsync(record.EventId, record.SequenceId, record.AggregateType,
                record.AggregateId, record.EventType, record.Payload, cancellationToken);
        }

        if (observer is not null)
        {
            await observer.BeforeMarkProcessedAsync(batch, cancellationToken);
        }

        await using (var mark = new NpgsqlCommand(
            "UPDATE outbox_events SET processed_at = clock_timestamp() WHERE event_id = ANY(@ids)",
            connection, transaction))
        {
            mark.Parameters.AddWithValue("ids", batch.Select(static record => record.EventId).ToArray());
            await mark.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return batch.Count;
    }
}
