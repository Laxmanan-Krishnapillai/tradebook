using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Tradebook.Core.Domain;
using Tradebook.Core.DTOs;
using Tradebook.Core.Messaging;
using Tradebook.Infrastructure.Data;
using Tradebook.IntegrationTests.Fixtures;
using Wolverine;
using Wolverine.Runtime;
using Wolverine.Tracking;
using Wolverine.Transports;

namespace Tradebook.IntegrationTests.RealTime;

[Trait("Category", "RealTime")]
public sealed class WolverineMessagingTests(PostgresTestFixture postgres)
    : PostgresDatabaseTestBase(postgres)
{
    private static readonly Guid TestActorId = Guid.Parse("3c7280d0-ab9e-48d1-87e9-848244111320");
    private static readonly JsonSerializerOptions WebJsonSerializerOptions = new(
        JsonSerializerDefaults.Web
    );

    private sealed record PushedEvent(
        Guid EventId,
        long SequenceId,
        string AggregateType,
        string AggregateId,
        string EventType,
        string PayloadJson
    );

    private sealed record DispatchLatencyBaseline(
        int SampleCount,
        double P99Milliseconds,
        DateTimeOffset CapturedAtUtc,
        string SourceRevision,
        string OperatingSystem,
        string Processor,
        string DockerVersion,
        IReadOnlyList<double> SamplesMilliseconds
    );

    [Fact]
    public async Task RolledBackCommandPersistsNoRowAuditOutgoingMessageOrRealtimeEffect()
    {
        var factory = CreateFactory();
        await using var configuredFactory = factory.ConfigureAwait(true);
        await ResetWolverineAsync(factory).ConfigureAwait(true);
        var priceDate = new DateOnly(2081, 1, 17);
        var aggregateId = priceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var (hub, events) = await ConnectAsync(factory, CreateToken()).ConfigureAwait(true);
        await using (hub.ConfigureAwait(true))
        {
            await hub.InvokeAsync("Subscribe", $"entity:{RealtimeAggregateTypes.MarketPrice}")
                .ConfigureAwait(true);

            await ExecuteMarketPriceCommandAsync(factory, priceDate, TestActorId, commit: false)
                .ConfigureAwait(true);
            await Task.Delay(250).ConfigureAwait(true);
            Assert.DoesNotContain(
                events,
                item => string.Equals(item.AggregateId, aggregateId, StringComparison.Ordinal)
            );
        }

        await AssertRolledBackStateAsync(priceDate, aggregateId).ConfigureAwait(true);
    }

    [Fact]
    public async Task DatabaseAuditTriggerCommitsWithTheCommandAndRollsBackWithIt()
    {
        var factory = CreateFactory();
        await using var configuredFactory = factory.ConfigureAwait(true);
        await ResetWolverineAsync(factory).ConfigureAwait(true);
        var committedDate = new DateOnly(2081, 2, 17);
        var rolledBackDate = new DateOnly(2081, 2, 18);

        await ExecuteMarketPriceCommandAsync(factory, committedDate, TestActorId, commit: true)
            .ConfigureAwait(true);
        await WaitForRealtimeLogAsync(
                committedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            )
            .ConfigureAwait(true);
        await ExecuteMarketPriceCommandAsync(factory, rolledBackDate, TestActorId, commit: false)
            .ConfigureAwait(true);

        await AssertAuditCommitAndRollbackStateAsync(committedDate, rolledBackDate)
            .ConfigureAwait(true);
    }

    [Fact]
    public async Task DuplicateEnvelopeIdProducesOneRealtimeEffectAndIsDiscardedByTheInbox()
    {
        var factory = CreateFactory();
        await using var configuredFactory = factory.ConfigureAwait(true);
        await ResetWolverineAsync(factory).ConfigureAwait(true);
        var aggregateId = Guid.NewGuid().ToString();
        var message = EntityChangedDomainEvent.Create(
            RealtimeAggregateTypes.PhysicalDelivery,
            aggregateId,
            "Created",
            version: 1
        );
        var envelopeId = Guid.Empty;
        var (hub, events) = await ConnectAsync(factory, CreateToken()).ConfigureAwait(true);
        await using (hub.ConfigureAwait(true))
        {
            await hub.InvokeAsync("Subscribe", $"entity:{RealtimeAggregateTypes.PhysicalDelivery}")
                .ConfigureAwait(true);

            var firstDispatch = await SendWithStartupGraceAsync(factory, message)
                .ConfigureAwait(true);
            var sentEnvelope = firstDispatch.Executed.SingleEnvelope<EntityChangedDomainEvent>();
            envelopeId = sentEnvelope.Id;
            Assert.Equal(
                message.EventId,
                Assert.IsType<EntityChangedDomainEvent>(sentEnvelope.Message).EventId
            );
            await WaitForAsync(events, item => item.EventId == message.EventId)
                .ConfigureAwait(true);
            await DeliverDuplicateEnvelopeAsync(factory, message, sentEnvelope)
                .ConfigureAwait(true);
            await Task.Delay(250).ConfigureAwait(true);
            Assert.Single(events, item => item.EventId == message.EventId);
        }

        await AssertDuplicateEnvelopeStateAsync(message.EventId, envelopeId).ConfigureAwait(true);
    }

    [Fact]
    public async Task DomainWriteReachesAMessagePackClientAndCatchUpResumesAfterItsSequence()
    {
        var factory = CreateFactory();
        await using var configuredFactory = factory.ConfigureAwait(true);
        await ResetWolverineAsync(factory).ConfigureAwait(true);
        var contractId = await SeedContractAsync().ConfigureAwait(true);
        var token = CreateToken();
        var (hub, events) = await ConnectAsync(factory, token).ConfigureAwait(true);
        long lastSeenSequence;
        Guid firstDeliveryId;
        await using (hub.ConfigureAwait(true))
        {
            (lastSeenSequence, firstDeliveryId) = await CreateFirstDeliveryAndCaptureSequenceAsync(
                    factory,
                    hub,
                    events,
                    token,
                    contractId
                )
                .ConfigureAwait(true);
        }

        await AssertCatchUpAfterSequenceAsync(
                factory,
                token,
                contractId,
                lastSeenSequence,
                firstDeliveryId
            )
            .ConfigureAwait(true);
    }

    [Fact]
    public async Task UnauthenticatedHubConnectionsAreRejected()
    {
        var factory = CreateFactory();
        await using var configuredFactory = factory.ConfigureAwait(true);
        var hub = BuildHubConnection(factory, token: null, out _);
        await using (hub.ConfigureAwait(true))
        {
            await Assert
                .ThrowsAsync<HttpRequestException>(() => hub.StartAsync())
                .ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task UnknownEntityGroupsAreRejected()
    {
        var factory = CreateFactory();
        await using var configuredFactory = factory.ConfigureAwait(true);
        var (hub, _) = await ConnectAsync(factory, CreateToken()).ConfigureAwait(true);
        await using (hub.ConfigureAwait(true))
        {
            await Assert
                .ThrowsAsync<HubException>(() => hub.InvokeAsync("Subscribe", "entity:Nope"))
                .ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task AnotherActorsDashboardGroupIsRejected()
    {
        var factory = CreateFactory();
        await using var configuredFactory = factory.ConfigureAwait(true);
        var (hub, _) = await ConnectAsync(factory, CreateToken()).ConfigureAwait(true);
        await using (hub.ConfigureAwait(true))
        {
            await Assert
                .ThrowsAsync<HubException>(() =>
                    hub.InvokeAsync("Subscribe", $"dashboard:{Guid.NewGuid()}")
                )
                .ConfigureAwait(true);
        }
    }

    [Fact]
    [Trait("Category", "MachineBaseline")]
    public async Task DispatchLatencyP99DoesNotRegressMoreThanTwentyPercentFromTheMeasuredBaseline()
    {
        var recordBaseline = string.Equals(
            Environment.GetEnvironmentVariable("TRADEBOOK_RECORD_DISPATCH_BASELINE"),
            "1",
            StringComparison.Ordinal
        );
        var baseline = recordBaseline
            ? null
            : await ReadDispatchLatencyBaselineAsync().ConfigureAwait(true);
        var sampleCount = baseline?.SampleCount ?? 20;

        var factory = CreateFactory();
        await using var configuredFactory = factory.ConfigureAwait(true);
        var samples = await MeasureDispatchLatenciesAsync(factory, sampleCount)
            .ConfigureAwait(true);
        var measuredP99 = Percentile99(samples);
        if (recordBaseline)
        {
            Console.WriteLine(
                "TASK17_DISPATCH_BASELINE="
                    + JsonSerializer.Serialize(
                        new
                        {
                            sampleCount,
                            p99Milliseconds = measuredP99,
                            samplesMilliseconds = samples,
                        },
                        WebJsonSerializerOptions
                    )
            );
            Assert.Fail(
                "Captured the Task 17 dispatch baseline. Commit the emitted measured "
                    + "samples and provenance before running the regression gate."
            );
            return;
        }

        Assert.NotNull(baseline);
        AssertDispatchLatencyWithinBaseline(baseline, measuredP99, samples);
    }

    private async Task AssertRolledBackStateAsync(DateOnly priceDate, string aggregateId)
    {
        var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await using var configuredConnection = connection.ConfigureAwait(false);
        Assert.Equal(
            0,
            await connection
                .ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM market_prices WHERE price_date = @PriceDate",
                    new { PriceDate = priceDate }
                )
                .ConfigureAwait(false)
        );
        Assert.Equal(
            0,
            await connection
                .ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM audit_log WHERE entity_name = 'market_prices' AND entity_id = @EntityId",
                    new { EntityId = aggregateId }
                )
                .ConfigureAwait(false)
        );
        Assert.Equal(
            0,
            await connection
                .ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM wolverine.wolverine_outgoing_envelopes"
                )
                .ConfigureAwait(false)
        );
        Assert.Equal(
            0,
            await connection
                .ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM wolverine.wolverine_incoming_envelopes"
                )
                .ConfigureAwait(false)
        );
        Assert.Equal(
            0,
            await connection
                .ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM realtime_event_log WHERE aggregate_id = @AggregateId",
                    new { AggregateId = aggregateId }
                )
                .ConfigureAwait(false)
        );
    }

    private async Task AssertAuditCommitAndRollbackStateAsync(
        DateOnly committedDate,
        DateOnly rolledBackDate
    )
    {
        var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await using var configuredConnection = connection.ConfigureAwait(false);
        await AssertAuditLogStateAsync(connection, committedDate, rolledBackDate)
            .ConfigureAwait(false);
        await AssertMarketPriceStateAsync(connection, committedDate, rolledBackDate)
            .ConfigureAwait(false);
    }

    private static async Task AssertAuditLogStateAsync(
        NpgsqlConnection connection,
        DateOnly committedDate,
        DateOnly rolledBackDate
    )
    {
        var auditRows = (
            await connection
                .QueryAsync<(string EntityId, Guid ActorId)>(
                    """
                    SELECT entity_id AS EntityId, actor_id AS ActorId
                    FROM audit_log
                    WHERE entity_name = 'market_prices'
                      AND entity_id = ANY(@EntityIds)
                    """,
                    new
                    {
                        EntityIds = new[]
                        {
                            committedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                            rolledBackDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        },
                    }
                )
                .ConfigureAwait(false)
        ).ToList();

        var audit = Assert.Single(auditRows);
        Assert.Equal(
            committedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            audit.EntityId
        );
        Assert.Equal(TestActorId, audit.ActorId);
    }

    private static async Task AssertMarketPriceStateAsync(
        NpgsqlConnection connection,
        DateOnly committedDate,
        DateOnly rolledBackDate
    )
    {
        Assert.Equal(
            1,
            await connection
                .ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM market_prices WHERE price_date = @PriceDate",
                    new { PriceDate = committedDate }
                )
                .ConfigureAwait(false)
        );
        Assert.Equal(
            0,
            await connection
                .ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM market_prices WHERE price_date = @PriceDate",
                    new { PriceDate = rolledBackDate }
                )
                .ConfigureAwait(false)
        );
        Assert.Equal(
            0,
            await connection
                .ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM realtime_event_log WHERE aggregate_id = @AggregateId",
                    new
                    {
                        AggregateId = rolledBackDate.ToString(
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture
                        ),
                    }
                )
                .ConfigureAwait(false)
        );
    }

    private async Task AssertDuplicateEnvelopeStateAsync(Guid eventId, Guid envelopeId)
    {
        var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await using var configuredConnection = connection.ConfigureAwait(false);
        Assert.Equal(
            1,
            await connection
                .ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM realtime_event_log WHERE event_id = @EventId",
                    new { EventId = eventId }
                )
                .ConfigureAwait(false)
        );
        Assert.Equal(
            1,
            await connection
                .ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM wolverine.wolverine_incoming_envelopes WHERE id = @EnvelopeId",
                    new { EnvelopeId = envelopeId }
                )
                .ConfigureAwait(false)
        );
        Assert.Equal(
            "Handled",
            await connection
                .QuerySingleAsync<string>(
                    "SELECT status FROM wolverine.wolverine_incoming_envelopes WHERE id = @EnvelopeId",
                    new { EnvelopeId = envelopeId }
                )
                .ConfigureAwait(false)
        );
    }

    private static async Task DeliverDuplicateEnvelopeAsync(
        WebApplicationFactory<Program> factory,
        EntityChangedDomainEvent message,
        Envelope sentEnvelope
    )
    {
        var runtime = factory.Services.GetRequiredService<IWolverineRuntime>();
        var destination =
            sentEnvelope.Destination
            ?? throw new InvalidOperationException("Tracked envelope has no destination.");
        var data =
            sentEnvelope.Data
            ?? throw new InvalidOperationException("Tracked envelope has no payload.");
        var duplicateEnvelope = new Envelope(message)
        {
            Id = sentEnvelope.Id,
            Destination = destination,
            MessageType = sentEnvelope.MessageType,
            ContentType = sentEnvelope.ContentType,
            Data = data.ToArray(),
        };
        foreach (var header in sentEnvelope.Headers)
        {
            duplicateEnvelope.Headers[header.Key] = header.Value;
        }

        var agent = runtime.Endpoints.AgentForLocalQueue(destination);
        Assert.NotNull(agent);
        var circuit = runtime.Endpoints.FindListenerCircuit(destination);
        Assert.NotNull(circuit);
        Assert.Equal(1, circuit.Endpoint.MaxDegreeOfParallelism);
        var receiver = Assert.IsAssignableFrom<IReceiver>(agent);
        var duplicateListener = new DuplicateDeliveryListener(destination);
        await receiver.ReceivedAsync(duplicateListener, duplicateEnvelope).ConfigureAwait(false);
        var completed = await duplicateListener
            .Completed.Task.WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);

        Assert.Same(duplicateEnvelope, completed);
        Assert.False(duplicateEnvelope.WasPersistedInInbox);
        Assert.False(duplicateListener.Deferred.Task.IsCompleted);
    }

    private static async Task<(
        long SequenceId,
        Guid DeliveryId
    )> CreateFirstDeliveryAndCaptureSequenceAsync(
        WebApplicationFactory<Program> factory,
        HubConnection hub,
        ConcurrentQueue<PushedEvent> events,
        string token,
        Guid contractId
    )
    {
        await hub.InvokeAsync("Subscribe", $"entity:{RealtimeAggregateTypes.PhysicalDelivery}")
            .ConfigureAwait(false);
        using var client = AuthenticatedClient(factory, token);
        var first = await CreateDeliveryAsync(client, contractId, new DateOnly(2082, 1, 1))
            .ConfigureAwait(false);
        var firstDeliveryId = first.DeliveryId.Value;
        var aggregateId = firstDeliveryId.ToString();
        var pushed = await WaitForAsync(
                events,
                item => string.Equals(item.AggregateId, aggregateId, StringComparison.Ordinal)
            )
            .ConfigureAwait(false);
        Assert.Equal(RealtimeAggregateTypes.PhysicalDelivery, pushed.AggregateType);
        Assert.Equal("Created", pushed.EventType);
        using (var payload = JsonDocument.Parse(pushed.PayloadJson))
        {
            Assert.Equal(aggregateId, payload.RootElement.GetProperty("aggregateId").GetString());
            Assert.Equal(first.Version, payload.RootElement.GetProperty("version").GetInt64());
        }

        return (pushed.SequenceId, firstDeliveryId);
    }

    private async Task AssertCatchUpAfterSequenceAsync(
        WebApplicationFactory<Program> factory,
        string token,
        Guid contractId,
        long lastSeenSequence,
        Guid firstDeliveryId
    )
    {
        using var reconnectingClient = AuthenticatedClient(factory, token);
        var second = await CreateDeliveryAsync(
                reconnectingClient,
                contractId,
                new DateOnly(2082, 2, 1)
            )
            .ConfigureAwait(false);
        var secondDeliveryId = second.DeliveryId.Value.ToString();
        await WaitForRealtimeLogAsync(secondDeliveryId).ConfigureAwait(false);
        var (reconnectedHub, _) = await ConnectAsync(factory, token).ConfigureAwait(false);
        await using (reconnectedHub.ConfigureAwait(false))
        {
            await reconnectedHub
                .InvokeAsync("Subscribe", $"entity:{RealtimeAggregateTypes.PhysicalDelivery}")
                .ConfigureAwait(false);
            var catchUp = await reconnectingClient
                .GetFromJsonAsync<GetEventsSinceResponse>(
                    $"/api/v1/events?afterSequence={lastSeenSequence}&limit=500"
                )
                .ConfigureAwait(false);

            Assert.NotNull(catchUp);
            var replayed = Assert.Single(
                catchUp.Events,
                item => string.Equals(item.AggregateId, secondDeliveryId, StringComparison.Ordinal)
            );
            Assert.True(replayed.SequenceId > lastSeenSequence);
            Assert.DoesNotContain(
                catchUp.Events,
                item =>
                    string.Equals(
                        item.AggregateId,
                        firstDeliveryId.ToString(),
                        StringComparison.Ordinal
                    )
            );
            Assert.True(catchUp.LatestSequence >= replayed.SequenceId);
        }
    }

    private static async Task<List<double>> MeasureDispatchLatenciesAsync(
        WebApplicationFactory<Program> factory,
        int sampleCount
    )
    {
        await ResetWolverineAsync(factory).ConfigureAwait(false);
        var token = CreateToken();
        var (hub, events) = await ConnectAsync(factory, token).ConfigureAwait(false);
        await using (hub.ConfigureAwait(false))
        {
            await hub.InvokeAsync("Subscribe", $"entity:{RealtimeAggregateTypes.MarketPrice}")
                .ConfigureAwait(false);
            using var client = AuthenticatedClient(factory, token);
            for (var index = 0; index < 3; index++)
            {
                await UpsertMarketPriceAndWaitAsync(
                        client,
                        events,
                        new DateOnly(2083, 1, 1).AddDays(index)
                    )
                    .ConfigureAwait(false);
            }

            var samples = new List<double>(sampleCount);
            for (var index = 0; index < sampleCount; index++)
            {
                var priceDate = new DateOnly(2083, 2, 1).AddDays(index);
                var stopwatch = Stopwatch.StartNew();
                await UpsertMarketPriceAndWaitAsync(client, events, priceDate)
                    .ConfigureAwait(false);
                stopwatch.Stop();
                samples.Add(stopwatch.Elapsed.TotalMilliseconds);
            }

            return samples;
        }
    }

    private static void AssertDispatchLatencyWithinBaseline(
        DispatchLatencyBaseline baseline,
        double measuredP99,
        IReadOnlyCollection<double> samples
    )
    {
        var regressionLimit = baseline.P99Milliseconds * 1.20;
        Console.WriteLine(
            $"Task 17 dispatch latency: measured p99={measuredP99:F2} ms; "
                + $"baseline={baseline.P99Milliseconds:F2} ms; 20% limit={regressionLimit:F2} ms; "
                + $"samples=[{string.Join(", ", samples.Select(value => value.ToString("F2", CultureInfo.InvariantCulture)))}]"
        );
        Assert.True(
            measuredP99 <= regressionLimit,
            $"Dispatch p99 {measuredP99:F2} ms regressed more than 20% from "
                + $"the measured {baseline.P99Milliseconds:F2} ms baseline."
        );
    }

    private static async Task<DispatchLatencyBaseline> ReadDispatchLatencyBaselineAsync()
    {
        var baselinePath = FindRepositoryFile("docs/baselines/task-17-dispatch-latency.json");
        var baselineJson = await File.ReadAllTextAsync(baselinePath).ConfigureAwait(false);
        var baseline = JsonSerializer.Deserialize<DispatchLatencyBaseline>(
            baselineJson,
            WebJsonSerializerOptions
        );
        Assert.NotNull(baseline);
        Assert.True(baseline.P99Milliseconds > 0);
        Assert.Equal(baseline.SampleCount, baseline.SamplesMilliseconds.Count);
        Assert.All(baseline.SamplesMilliseconds, sample => Assert.True(sample > 0));
        Assert.NotEqual(default, baseline.CapturedAtUtc);
        Assert.False(string.IsNullOrWhiteSpace(baseline.SourceRevision));
        Assert.False(string.IsNullOrWhiteSpace(baseline.OperatingSystem));
        Assert.False(string.IsNullOrWhiteSpace(baseline.Processor));
        Assert.False(string.IsNullOrWhiteSpace(baseline.DockerVersion));
        return baseline;
    }

    private static async Task ExecuteMarketPriceCommandAsync(
        WebApplicationFactory<Program> factory,
        DateOnly priceDate,
        Guid actorId,
        bool commit
    )
    {
        var scope = factory.Services.CreateAsyncScope();
        await using var configuredScope = scope.ConfigureAwait(false);
        var connections = scope.ServiceProvider.GetRequiredService<INpgsqlConnectionFactory>();
        var publisher = scope.ServiceProvider.GetRequiredService<ITransactionalEventPublisher>();
        var connection = await connections.OpenConnectionAsync(default).ConfigureAwait(false);
        await using var configuredConnection = connection.ConfigureAwait(false);
        var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);
        await using var configuredTransaction = transaction.ConfigureAwait(false);
        try
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
            var version = await connection
                .ExecuteScalarAsync<long>(
                    new CommandDefinition(
                        """
                        INSERT INTO market_prices (price_date, ttf_eur_mwh)
                        VALUES (@PriceDate, 31.5)
                        RETURNING version
                        """,
                        new { PriceDate = priceDate },
                        transaction
                    )
                )
                .ConfigureAwait(false);
            await publisher.EnlistAsync(transaction, default).ConfigureAwait(false);
            await publisher
                .PublishAsync(
                    EntityChangedDomainEvent.Create(
                        RealtimeAggregateTypes.MarketPrice,
                        priceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        "Created",
                        version
                    )
                )
                .ConfigureAwait(false);

            if (!commit)
            {
                throw new InjectedCommandFailureException();
            }

            await transaction.CommitAsync().ConfigureAwait(false);
            await publisher.FlushAsync().ConfigureAwait(false);
        }
        catch (InjectedCommandFailureException)
        {
            await transaction.RollbackAsync().ConfigureAwait(false);
        }
    }

    private async Task WaitForRealtimeLogAsync(string aggregateId)
    {
        var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await using var configuredConnection = connection.ConfigureAwait(false);
        var deadline = TimeProvider.System.GetUtcNow().UtcDateTime + TimeSpan.FromSeconds(15);
        while (TimeProvider.System.GetUtcNow().UtcDateTime < deadline)
        {
            if (
                await connection
                    .ExecuteScalarAsync<bool>(
                        "SELECT EXISTS(SELECT 1 FROM realtime_event_log WHERE aggregate_id = @AggregateId)",
                        new { AggregateId = aggregateId }
                    )
                    .ConfigureAwait(false)
            )
            {
                return;
            }

            await Task.Delay(50).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"No realtime event for aggregate '{aggregateId}' arrived within 15 seconds."
        );
    }

    private async Task<Guid> SeedContractAsync()
    {
        var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await using var configuredConnection = connection.ConfigureAwait(false);
        var counterpartyId = Guid.NewGuid();
        await connection
            .ExecuteAsync(
                "INSERT INTO counterparties (id, name, shorthand) VALUES (@Id, @Name, @Shorthand)",
                new
                {
                    Id = counterpartyId,
                    Name = $"Counterparty-{counterpartyId}",
                    Shorthand = $"CP{counterpartyId:N}"[..20],
                }
            )
            .ConfigureAwait(false);
        var contractId = Guid.NewGuid();
        await connection
            .ExecuteAsync(
                """
                INSERT INTO contracts (id, contract_name, counterparty_id, product_type, action)
                VALUES (@Id, @Name, @CounterpartyId, 'Gas', 'Sell')
                """,
                new
                {
                    Id = contractId,
                    Name = $"TEST45.SG.{contractId:N}"[..40],
                    CounterpartyId = counterpartyId,
                }
            )
            .ConfigureAwait(false);
        return contractId;
    }

    private static async Task<CreatePhysicalDeliveryResponse> CreateDeliveryAsync(
        HttpClient client,
        Guid contractId,
        DateOnly supplyMonth
    )
    {
        using var response = await client
            .PostAsJsonAsync(
                "/api/v1/deliveries",
                new
                {
                    contractId,
                    bookType = "Sales",
                    supplyMonth = supplyMonth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    volumeNominatedMwh = "10",
                    volumeRealisedMwh = "9",
                    priceMechanism = "TTF",
                }
            )
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (
            await response
                .Content.ReadFromJsonAsync<CreatePhysicalDeliveryResponse>()
                .ConfigureAwait(false)
        )!;
    }

    private static async Task UpsertMarketPriceAndWaitAsync(
        HttpClient client,
        ConcurrentQueue<PushedEvent> events,
        DateOnly priceDate
    )
    {
        var aggregateId = priceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        using var response = await client
            .PutAsJsonAsync(
                $"/api/v1/market-prices/{aggregateId}",
                new
                {
                    priceDate = aggregateId,
                    ttfEurMwh = "31.5",
                    version = 0,
                }
            )
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await WaitForAsync(
                events,
                item => string.Equals(item.AggregateId, aggregateId, StringComparison.Ordinal)
            )
            .ConfigureAwait(false);
    }

    private static double Percentile99(IReadOnlyCollection<double> samples)
    {
        var ordered = samples.Order().ToArray();
        var position = (ordered.Length - 1) * 0.99;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        return ordered[lower] + ((ordered[upper] - ordered[lower]) * (position - lower));
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Database:ConnectionString", Postgres.ConnectionString);
            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>(StringComparer.Ordinal)
                        {
                            ["Database:ConnectionString"] = Postgres.ConnectionString,
                            ["Entra:TenantId"] = "11111111-1111-1111-1111-111111111111",
                            ["Entra:ClientId"] = "22222222-2222-2222-2222-222222222222",
                        }
                    )
            );
        });

    private static async Task<ITrackedSession> SendWithStartupGraceAsync(
        WebApplicationFactory<Program> factory,
        EntityChangedDomainEvent message
    )
    {
        // Wolverine starts on a background retry loop; the tracked send waits out a
        // racing deferred start the same way the production publisher does.
        var attempt = 0;
        while (true)
        {
            try
            {
                return await factory
                    .Services.SendMessageAndWaitAsync(message, timeoutInMilliseconds: 15_000)
                    .ConfigureAwait(true);
            }
            catch (WolverineHasNotStartedException) when (attempt < 300)
            {
                attempt++;
                await Task.Delay(100).ConfigureAwait(true);
            }
        }
    }

    private static async Task ResetWolverineAsync(WebApplicationFactory<Program> factory)
    {
        var runtime = factory.Services.GetRequiredService<IWolverineRuntime>();
        // Wolverine now starts on a background retry loop, so the envelope schema may
        // not exist yet when a test resets storage; wait for the deferred start.
        var attempt = 0;
        while (true)
        {
            try
            {
                await runtime.Storage.Admin.ClearAllAsync().ConfigureAwait(false);
                return;
            }
            catch (Exception) when (attempt < 300)
            {
                attempt++;
                await Task.Delay(100).ConfigureAwait(false);
            }
        }
    }

    private static HttpClient AuthenticatedClient(
        WebApplicationFactory<Program> factory,
        string token
    )
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static HubConnection BuildHubConnection(
        WebApplicationFactory<Program> factory,
        string? token,
        out ConcurrentQueue<PushedEvent> events
    )
    {
        events = new ConcurrentQueue<PushedEvent>();
        var sink = events;
        var hub = new HubConnectionBuilder()
            .WithUrl(
                new Uri(factory.Server.BaseAddress, "hubs/dashboard"),
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                    if (token is not null)
                    {
                        options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                    }
                }
            )
            .AddMessagePackProtocol()
            .Build();
        hub.On<Guid, long, string, string, string, string>(
            "EntityChanged",
            (eventId, sequenceId, aggregateType, aggregateId, eventType, payloadJson) =>
                sink.Enqueue(
                    new PushedEvent(
                        eventId,
                        sequenceId,
                        aggregateType,
                        aggregateId,
                        eventType,
                        payloadJson
                    )
                )
        );
        return hub;
    }

    private static async Task<(
        HubConnection Hub,
        ConcurrentQueue<PushedEvent> Events
    )> ConnectAsync(WebApplicationFactory<Program> factory, string token)
    {
        var hub = BuildHubConnection(factory, token, out var events);
        await hub.StartAsync().ConfigureAwait(false);
        return (hub, events);
    }

    private static async Task<PushedEvent> WaitForAsync(
        ConcurrentQueue<PushedEvent> events,
        Func<PushedEvent, bool> predicate
    )
    {
        var deadline = TimeProvider.System.GetUtcNow().UtcDateTime + TimeSpan.FromSeconds(15);
        while (TimeProvider.System.GetUtcNow().UtcDateTime < deadline)
        {
            var match = events.FirstOrDefault(predicate);
            if (match is not null)
            {
                return match;
            }

            await Task.Delay(50).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"No matching SignalR event arrived within 15 seconds ({events.Count} received)."
        );
    }

    private static string CreateToken()
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(CustomWebApplicationFactory.JwtSigningKey)
        );
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "Tradebook",
            Audience = "Tradebook",
            Subject = new ClaimsIdentity([
                new Claim("oid", TestActorId.ToString()),
                new Claim(ClaimTypes.Role, "Admin"),
            ]),
            Expires = TimeProvider.System.GetUtcNow().UtcDateTime.AddMinutes(10),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

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

    public sealed class InjectedCommandFailureException : Exception;

    private sealed class DuplicateDeliveryListener(Uri address) : IListener
    {
        public TaskCompletionSource<Envelope> Completed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<Envelope> Deferred { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Uri Address { get; } = address;
        public IHandlerPipeline? Pipeline => null;

        public ValueTask CompleteAsync(Envelope envelope)
        {
            Completed.TrySetResult(envelope);
            return ValueTask.CompletedTask;
        }

        public ValueTask DeferAsync(Envelope envelope)
        {
            Deferred.TrySetResult(envelope);
            return ValueTask.CompletedTask;
        }

        public ValueTask StopAsync() => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
