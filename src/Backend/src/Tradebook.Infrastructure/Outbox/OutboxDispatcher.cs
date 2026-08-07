using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Tradebook.Core.Interfaces;
using Tradebook.Infrastructure.Data;

namespace Tradebook.Infrastructure.Outbox;

/// <summary>
/// Coalesces PostgreSQL NOTIFY signals into a bounded in-process channel, then drains
/// the transactional outbox to SignalR. PostgreSQL remains the source of truth, so a
/// capacity-one wake channel is sufficient and applies bounded backpressure.
/// </summary>
public sealed class OutboxDispatcher : BackgroundService
{
    private readonly INpgsqlConnectionFactory _connections;
    private readonly IOutboxEventFanout _fanout;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxDispatcher> _logger;
    private readonly IOutboxDispatchObserver? _observer;
    private readonly Channel<byte> _wakeups = Channel.CreateBounded<byte>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });

    public OutboxDispatcher(
        INpgsqlConnectionFactory connections,
        IOutboxEventFanout fanout,
        IOptions<OutboxOptions> options,
        ILogger<OutboxDispatcher> logger,
        IOutboxDispatchObserver? observer = null)
    {
        _connections = connections;
        _fanout = fanout;
        _options = options.Value;
        _logger = logger;
        _observer = observer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listener = ListenForWakeupsAsync(stoppingToken);
        var dispatcher = ConsumeWakeupsAsync(stoppingToken);
        await Task.WhenAll(listener, dispatcher);
    }

    private async Task ListenForWakeupsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var listenConnection = await _connections.OpenConnectionAsync(cancellationToken);
                await using (var listen = new NpgsqlCommand("LISTEN outbox_new_event", listenConnection))
                {
                    await listen.ExecuteNonQueryAsync(cancellationToken);
                }

                SignalWakeup();
                while (!cancellationToken.IsCancellationRequested)
                {
                    // A timeout provides the correctness fallback when a NOTIFY is lost.
                    await listenConnection.WaitAsync(
                        TimeSpan.FromSeconds(_options.FallbackPollSeconds),
                        cancellationToken);
                    SignalWakeup();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Outbox LISTEN connection failed; backing off {BackoffSeconds}s before reconnecting.",
                    _options.ErrorBackoffSeconds);
                await BackoffAsync(cancellationToken);
            }
        }
    }

    private async Task ConsumeWakeupsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _wakeups.Reader.WaitToReadAsync(cancellationToken))
            {
                while (_wakeups.Reader.TryRead(out _))
                {
                    // Coalesce all pending wake signals before draining the source table.
                }

                try
                {
                    while (await DispatchBatchAsync(cancellationToken) > 0)
                    {
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Outbox dispatch failed; backing off {BackoffSeconds}s before retrying.",
                        _options.ErrorBackoffSeconds);
                    await BackoffAsync(cancellationToken);
                    SignalWakeup();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void SignalWakeup() => _wakeups.Writer.TryWrite(0);

    private async Task BackoffAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(_options.ErrorBackoffSeconds), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connections.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var batch = new List<OutboxEventRecord>();
        await using (var claim = new NpgsqlCommand("""
            SELECT event_id, sequence_id, aggregate_type, aggregate_id, event_type, payload::text
            FROM outbox_events
            WHERE processed_at IS NULL
            ORDER BY sequence_id
            LIMIT @batchSize
            FOR UPDATE SKIP LOCKED
            """, connection, transaction))
        {
            claim.Parameters.AddWithValue("batchSize", _options.BatchSize);
            await using var reader = await claim.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                batch.Add(new OutboxEventRecord(
                    reader.GetGuid(0),
                    reader.GetInt64(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5)));
            }
        }

        if (batch.Count == 0)
        {
            return 0;
        }

        foreach (var record in batch)
        {
            await _fanout.PublishEntityChangedAsync(
                record.EventId,
                record.SequenceId,
                record.AggregateType,
                record.AggregateId,
                record.EventType,
                record.Payload,
                cancellationToken);
        }

        if (_observer is not null)
        {
            await _observer.BeforeMarkProcessedAsync(batch, cancellationToken);
        }

        await using (var mark = new NpgsqlCommand(
            "UPDATE outbox_events SET processed_at = clock_timestamp() WHERE event_id = ANY(@ids)",
            connection,
            transaction))
        {
            mark.Parameters.AddWithValue("ids", batch.Select(static record => record.EventId).ToArray());
            await mark.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return batch.Count;
    }
}
