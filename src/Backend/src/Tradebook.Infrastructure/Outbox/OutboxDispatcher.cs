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
public sealed partial class OutboxDispatcher : BackgroundService
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
            SingleWriter = false,
        }
    );

    public OutboxDispatcher(
        INpgsqlConnectionFactory connections,
        IOutboxEventFanout fanout,
        IOptions<OutboxOptions> options,
        ILogger<OutboxDispatcher> logger,
        IOutboxDispatchObserver? observer = null
    )
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
        await Task.WhenAll(listener, dispatcher).ConfigureAwait(false);
    }

    private async Task ListenForWakeupsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var listenConnection = await _connections
                    .OpenConnectionAsync(cancellationToken)
                    .ConfigureAwait(false);
                await using (listenConnection.ConfigureAwait(false))
                {
                    var listen = new NpgsqlCommand("LISTEN outbox_new_event", listenConnection);
                    await using (listen.ConfigureAwait(false))
                    {
                        await listen.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }

                    SignalWakeup();
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        // A timeout provides the correctness fallback when a NOTIFY is lost.
                        await listenConnection
                            .WaitAsync(
                                TimeSpan.FromSeconds(_options.FallbackPollSeconds),
                                cancellationToken
                            )
                            .ConfigureAwait(false);
                        SignalWakeup();
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogListenFailure(_logger, exception, _options.ErrorBackoffSeconds);
                await BackoffAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ConsumeWakeupsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _wakeups.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                // Capacity is one, so consuming a single item drains every coalesced wake signal.
                _wakeups.Reader.TryRead(out _);

                try
                {
                    var dispatchedCount = await DispatchBatchAsync(cancellationToken)
                        .ConfigureAwait(false);
                    while (dispatchedCount > 0)
                    {
                        dispatchedCount = await DispatchBatchAsync(cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    LogDispatchFailure(_logger, exception, _options.ErrorBackoffSeconds);
                    await BackoffAsync(cancellationToken).ConfigureAwait(false);
                    SignalWakeup();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogCancellation(_logger);
        }
    }

    private void SignalWakeup() => _wakeups.Writer.TryWrite(0);

    private async Task BackoffAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(_options.ErrorBackoffSeconds), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        var connection = await _connections
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                var batch = await ClaimBatchAsync(
                        connection,
                        transaction,
                        _options.BatchSize,
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                if (batch.Count == 0)
                {
                    return 0;
                }

                await PublishBatchAsync(batch, cancellationToken).ConfigureAwait(false);

                await MarkProcessedAsync(connection, transaction, batch, cancellationToken)
                    .ConfigureAwait(false);

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return batch.Count;
            }
        }
    }

    private static async Task<IReadOnlyList<OutboxEventRecord>> ClaimBatchAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int batchSize,
        CancellationToken cancellationToken
    )
    {
        var batch = new List<OutboxEventRecord>();
        var claim = new NpgsqlCommand(
            """
            SELECT event_id, sequence_id, aggregate_type, aggregate_id, event_type, payload::text
            FROM outbox_events
            WHERE processed_at IS NULL
            ORDER BY sequence_id
            LIMIT @batchSize
            FOR UPDATE SKIP LOCKED
            """,
            connection,
            transaction
        );
        await using (claim.ConfigureAwait(false))
        {
            claim.Parameters.AddWithValue("batchSize", batchSize);
            var reader = await claim.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await (reader.ReadAsync(cancellationToken)).ConfigureAwait(false))
                {
                    batch.Add(
                        new OutboxEventRecord(
                            reader.GetGuid(0),
                            reader.GetInt64(1),
                            reader.GetString(2),
                            reader.GetString(3),
                            reader.GetString(4),
                            reader.GetString(5)
                        )
                    );
                }
            }
        }

        return batch;
    }

    private async Task PublishBatchAsync(
        IReadOnlyList<OutboxEventRecord> batch,
        CancellationToken cancellationToken
    )
    {
        foreach (var record in batch)
        {
            await _fanout
                .PublishEntityChangedAsync(
                    record.EventId,
                    record.SequenceId,
                    record.AggregateType,
                    record.AggregateId,
                    record.EventType,
                    record.Payload,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        if (_observer is not null)
        {
            await _observer
                .BeforeMarkProcessedAsync(batch, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task MarkProcessedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyCollection<OutboxEventRecord> batch,
        CancellationToken cancellationToken
    )
    {
        var mark = new NpgsqlCommand(
            "UPDATE outbox_events SET processed_at = clock_timestamp() WHERE event_id = ANY(@ids)",
            connection,
            transaction
        );
        await using (mark.ConfigureAwait(false))
        {
            mark.Parameters.AddWithValue(
                "ids",
                batch.Select(static record => record.EventId).ToArray()
            );
            await mark.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Outbox LISTEN connection failed; backing off {BackoffSeconds}s before reconnecting."
    )]
    private static partial void LogListenFailure(
        ILogger logger,
        Exception exception,
        int backoffSeconds
    );

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Outbox dispatch failed; backing off {BackoffSeconds}s before retrying."
    )]
    private static partial void LogDispatchFailure(
        ILogger logger,
        Exception exception,
        int backoffSeconds
    );

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Debug,
        Message = "Outbox dispatcher stopped because cancellation was requested."
    )]
    private static partial void LogCancellation(ILogger logger);
}
