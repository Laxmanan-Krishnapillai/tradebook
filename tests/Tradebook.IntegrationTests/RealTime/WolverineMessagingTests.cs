using System.Collections.Concurrent;
using System.Diagnostics;
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
using Tradebook.IntegrationTests.Fixtures;
using Tradebook.Infrastructure.Data;
using Wolverine;
using Wolverine.Runtime;
using Wolverine.Transports;
using Wolverine.Tracking;

namespace Tradebook.IntegrationTests.RealTime;

[Trait("Category", "RealTime")]
public sealed class WolverineMessagingTests(PostgresTestFixture postgres)
    : PostgresDatabaseTestBase(postgres)
{
    private static readonly Guid TestActorId =
        Guid.Parse("3c7280d0-ab9e-48d1-87e9-848244111320");

    private sealed record PushedEvent(
        Guid EventId,
        long SequenceId,
        string AggregateType,
        string AggregateId,
        string EventType,
        string PayloadJson);

    private sealed record DispatchLatencyBaseline(
        int SampleCount,
        double P99Milliseconds,
        DateTimeOffset CapturedAtUtc,
        string SourceRevision,
        string OperatingSystem,
        string Processor,
        string DockerVersion,
        IReadOnlyList<double> SamplesMilliseconds);

    [Fact]
    public async Task Rolled_back_command_persists_no_row_audit_outgoing_message_or_realtime_effect()
    {
        await using var factory = CreateFactory();
        await ResetWolverineAsync(factory);
        var priceDate = new DateOnly(2081, 1, 17);
        var aggregateId = priceDate.ToString("yyyy-MM-dd");
        var (hub, events) = await ConnectAsync(factory, CreateToken());
        await using (hub)
        {
            await hub.InvokeAsync("Subscribe", $"entity:{RealtimeAggregateTypes.MarketPrice}");

            await ExecuteMarketPriceCommandAsync(
                factory,
                priceDate,
                TestActorId,
                commit: false);
            await Task.Delay(250);
            Assert.DoesNotContain(events, item => item.AggregateId == aggregateId);
        }

        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM market_prices WHERE price_date = @PriceDate",
            new { PriceDate = priceDate }));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM audit_log WHERE entity_name = 'market_prices' AND entity_id = @EntityId",
            new { EntityId = priceDate.ToString("yyyy-MM-dd") }));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM wolverine.wolverine_outgoing_envelopes"));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM wolverine.wolverine_incoming_envelopes"));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM realtime_event_log WHERE aggregate_id = @AggregateId",
            new { AggregateId = aggregateId }));
    }

    [Fact]
    public async Task Database_audit_trigger_commits_with_the_command_and_rolls_back_with_it()
    {
        await using var factory = CreateFactory();
        await ResetWolverineAsync(factory);
        var committedDate = new DateOnly(2081, 2, 17);
        var rolledBackDate = new DateOnly(2081, 2, 18);

        await ExecuteMarketPriceCommandAsync(
            factory,
            committedDate,
            TestActorId,
            commit: true);
        await WaitForRealtimeLogAsync(committedDate.ToString("yyyy-MM-dd"));
        await ExecuteMarketPriceCommandAsync(
            factory,
            rolledBackDate,
            TestActorId,
            commit: false);

        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        var auditRows = (await connection.QueryAsync<(string EntityId, Guid ActorId)>(
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
                    committedDate.ToString("yyyy-MM-dd"),
                    rolledBackDate.ToString("yyyy-MM-dd"),
                },
            })).ToList();

        var audit = Assert.Single(auditRows);
        Assert.Equal(committedDate.ToString("yyyy-MM-dd"), audit.EntityId);
        Assert.Equal(TestActorId, audit.ActorId);
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM market_prices WHERE price_date = @PriceDate",
            new { PriceDate = committedDate }));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM market_prices WHERE price_date = @PriceDate",
            new { PriceDate = rolledBackDate }));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM realtime_event_log WHERE aggregate_id = @AggregateId",
            new { AggregateId = rolledBackDate.ToString("yyyy-MM-dd") }));
    }

    [Fact]
    public async Task Duplicate_envelope_id_produces_one_realtime_effect_and_is_discarded_by_the_inbox()
    {
        await using var factory = CreateFactory();
        await ResetWolverineAsync(factory);
        var aggregateId = Guid.NewGuid().ToString();
        var message = EntityChangedDomainEvent.Create(
            RealtimeAggregateTypes.PhysicalDelivery,
            aggregateId,
            "Created",
            version: 1);
        var envelopeId = Guid.Empty;
        var (hub, events) = await ConnectAsync(factory, CreateToken());
        await using (hub)
        {
            await hub.InvokeAsync("Subscribe", $"entity:{RealtimeAggregateTypes.PhysicalDelivery}");

            var firstDispatch = await factory.Services.SendMessageAndWaitAsync(
                message,
                timeoutInMilliseconds: 15_000);
            var sentEnvelope = firstDispatch.Executed
                .SingleEnvelope<EntityChangedDomainEvent>();
            envelopeId = sentEnvelope.Id;
            Assert.Equal(message.EventId,
                Assert.IsType<EntityChangedDomainEvent>(sentEnvelope.Message).EventId);
            await WaitForAsync(events, item => item.EventId == message.EventId);

            var runtime = factory.Services.GetRequiredService<IWolverineRuntime>();
            var destination = sentEnvelope.Destination
                ?? throw new InvalidOperationException("Tracked envelope has no destination.");
            var data = sentEnvelope.Data
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
            await receiver.ReceivedAsync(duplicateListener, duplicateEnvelope);
            var completed = await duplicateListener.Completed.Task
                .WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Same(duplicateEnvelope, completed);
            Assert.False(duplicateEnvelope.WasPersistedInInbox);
            Assert.False(duplicateListener.Deferred.Task.IsCompleted);
            await Task.Delay(250);
            Assert.Single(events, item => item.EventId == message.EventId);
        }

        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM realtime_event_log WHERE event_id = @EventId",
            new { message.EventId }));
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM wolverine.wolverine_incoming_envelopes WHERE id = @EnvelopeId",
            new { EnvelopeId = envelopeId }));
        Assert.Equal("Handled", await connection.QuerySingleAsync<string>(
            "SELECT status FROM wolverine.wolverine_incoming_envelopes WHERE id = @EnvelopeId",
            new { EnvelopeId = envelopeId }));
    }

    [Fact]
    public async Task Domain_write_reaches_a_MessagePack_client_and_catch_up_resumes_after_its_sequence()
    {
        await using var factory = CreateFactory();
        await ResetWolverineAsync(factory);
        var contractId = await SeedContractAsync();
        var token = CreateToken();
        var (hub, events) = await ConnectAsync(factory, token);
        long lastSeenSequence;
        Guid firstDeliveryId;
        await using (hub)
        {
            await hub.InvokeAsync("Subscribe", $"entity:{RealtimeAggregateTypes.PhysicalDelivery}");
            using var client = AuthenticatedClient(factory, token);

            var first = await CreateDeliveryAsync(client, contractId, new DateOnly(2082, 1, 1));
            firstDeliveryId = first.DeliveryId;
            var pushed = await WaitForAsync(
                events,
                item => item.AggregateId == first.DeliveryId.ToString());
            Assert.Equal(RealtimeAggregateTypes.PhysicalDelivery, pushed.AggregateType);
            Assert.Equal("Created", pushed.EventType);
            using (var payload = JsonDocument.Parse(pushed.PayloadJson))
            {
                Assert.Equal(first.DeliveryId.ToString(),
                    payload.RootElement.GetProperty("aggregateId").GetString());
                Assert.Equal(first.Version, payload.RootElement.GetProperty("version").GetInt64());
            }

            lastSeenSequence = pushed.SequenceId;
        }

        using var reconnectingClient = AuthenticatedClient(factory, token);
        var second = await CreateDeliveryAsync(
            reconnectingClient,
            contractId,
            new DateOnly(2082, 2, 1));
        await WaitForRealtimeLogAsync(second.DeliveryId.ToString());
        var (reconnectedHub, _) = await ConnectAsync(factory, token);
        await using (reconnectedHub)
        {
            await reconnectedHub.InvokeAsync(
                "Subscribe",
                $"entity:{RealtimeAggregateTypes.PhysicalDelivery}");
            var catchUp = await reconnectingClient.GetFromJsonAsync<GetEventsSinceResponse>(
                $"/api/v1/events?afterSequence={lastSeenSequence}&limit=500");

            Assert.NotNull(catchUp);
            var replayed = Assert.Single(
                catchUp.Events,
                item => item.AggregateId == second.DeliveryId.ToString());
            Assert.True(replayed.SequenceId > lastSeenSequence);
            Assert.DoesNotContain(catchUp.Events,
                item => item.AggregateId == firstDeliveryId.ToString());
            Assert.True(catchUp.LatestSequence >= replayed.SequenceId);
        }
    }

    [Fact]
    public async Task Unauthenticated_hub_connections_are_rejected()
    {
        await using var factory = CreateFactory();
        var hub = BuildHubConnection(factory, token: null, out _);
        await using (hub)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => hub.StartAsync());
        }
    }

    [Fact]
    public async Task Unknown_entity_groups_are_rejected()
    {
        await using var factory = CreateFactory();
        var (hub, _) = await ConnectAsync(factory, CreateToken());
        await using (hub)
        {
            await Assert.ThrowsAsync<HubException>(() =>
                hub.InvokeAsync("Subscribe", "entity:Nope"));
        }
    }

    [Fact]
    public async Task Another_actors_dashboard_group_is_rejected()
    {
        await using var factory = CreateFactory();
        var (hub, _) = await ConnectAsync(factory, CreateToken());
        await using (hub)
        {
            await Assert.ThrowsAsync<HubException>(() =>
                hub.InvokeAsync("Subscribe", $"dashboard:{Guid.NewGuid()}"));
        }
    }

    [Fact]
    public async Task Dispatch_latency_p99_does_not_regress_more_than_twenty_percent_from_the_measured_baseline()
    {
        var recordBaseline = string.Equals(
            Environment.GetEnvironmentVariable("TRADEBOOK_RECORD_DISPATCH_BASELINE"),
            "1",
            StringComparison.Ordinal);
        var baseline = recordBaseline ? null : await ReadDispatchLatencyBaselineAsync();
        var sampleCount = baseline?.SampleCount ?? 20;

        await using var factory = CreateFactory();
        await ResetWolverineAsync(factory);
        var token = CreateToken();
        var (hub, events) = await ConnectAsync(factory, token);
        await using (hub)
        {
            await hub.InvokeAsync("Subscribe", $"entity:{RealtimeAggregateTypes.MarketPrice}");
            using var client = AuthenticatedClient(factory, token);
            for (var index = 0; index < 3; index++)
            {
                await UpsertMarketPriceAndWaitAsync(
                    client,
                    events,
                    new DateOnly(2083, 1, 1).AddDays(index));
            }

            var samples = new List<double>(sampleCount);
            for (var index = 0; index < sampleCount; index++)
            {
                var priceDate = new DateOnly(2083, 2, 1).AddDays(index);
                var stopwatch = Stopwatch.StartNew();
                await UpsertMarketPriceAndWaitAsync(client, events, priceDate);
                stopwatch.Stop();
                samples.Add(stopwatch.Elapsed.TotalMilliseconds);
            }

            var measuredP99 = Percentile99(samples);
            if (recordBaseline)
            {
                Console.WriteLine(
                    "TASK17_DISPATCH_BASELINE=" + JsonSerializer.Serialize(new
                    {
                        sampleCount,
                        p99Milliseconds = measuredP99,
                        samplesMilliseconds = samples,
                    }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
                Assert.Fail(
                    "Captured the Task 17 dispatch baseline. Commit the emitted measured " +
                    "samples and provenance before running the regression gate.");
                return;
            }

            Assert.NotNull(baseline);
            var regressionLimit = baseline.P99Milliseconds * 1.20;
            Console.WriteLine(
                $"Task 17 dispatch latency: measured p99={measuredP99:F2} ms; " +
                $"baseline={baseline.P99Milliseconds:F2} ms; 20% limit={regressionLimit:F2} ms; " +
                $"samples=[{string.Join(", ", samples.Select(value => value.ToString("F2")))}]");
            Assert.True(
                measuredP99 <= regressionLimit,
                $"Dispatch p99 {measuredP99:F2} ms regressed more than 20% from " +
                $"the measured {baseline.P99Milliseconds:F2} ms baseline.");
        }
    }

    private static async Task<DispatchLatencyBaseline> ReadDispatchLatencyBaselineAsync()
    {
        var baselinePath = FindRepositoryFile(
            "docs/baselines/task-17-dispatch-latency.json");
        var baseline = JsonSerializer.Deserialize<DispatchLatencyBaseline>(
            await File.ReadAllTextAsync(baselinePath),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
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

    private async Task ExecuteMarketPriceCommandAsync(
        WebApplicationFactory<Program> factory,
        DateOnly priceDate,
        Guid actorId,
        bool commit)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var connections = scope.ServiceProvider
            .GetRequiredService<INpgsqlConnectionFactory>();
        var publisher = scope.ServiceProvider
            .GetRequiredService<ITransactionalEventPublisher>();
        await using var connection = await connections.OpenConnectionAsync(default);
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "SELECT set_config('app.actor_id', @ActorId, true)",
                new { ActorId = actorId.ToString() },
                transaction));
            var version = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                INSERT INTO market_prices (price_date, ttf_eur_mwh)
                VALUES (@PriceDate, 31.5)
                RETURNING version
                """,
                new { PriceDate = priceDate },
                transaction));
            await publisher.EnlistAsync(transaction, default);
            await publisher.PublishAsync(EntityChangedDomainEvent.Create(
                RealtimeAggregateTypes.MarketPrice,
                priceDate.ToString("yyyy-MM-dd"),
                "Created",
                version));

            if (!commit)
            {
                throw new InjectedCommandFailureException();
            }

            await transaction.CommitAsync();
            await publisher.FlushAsync();
        }
        catch (InjectedCommandFailureException)
        {
            await transaction.RollbackAsync();
        }
    }

    private async Task WaitForRealtimeLogAsync(string aggregateId)
    {
        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (await connection.ExecuteScalarAsync<bool>(
                    "SELECT EXISTS(SELECT 1 FROM realtime_event_log WHERE aggregate_id = @AggregateId)",
                    new { AggregateId = aggregateId }))
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException(
            $"No realtime event for aggregate '{aggregateId}' arrived within 15 seconds.");
    }

    private async Task<Guid> SeedContractAsync()
    {
        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        var counterpartyId = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO counterparties (id, name, shorthand) VALUES (@Id, @Name, @Shorthand)",
            new
            {
                Id = counterpartyId,
                Name = $"Counterparty-{counterpartyId}",
                Shorthand = $"CP{counterpartyId:N}"[..20],
            });
        var contractId = Guid.NewGuid();
        await connection.ExecuteAsync(
            """
            INSERT INTO contracts (id, contract_name, counterparty_id, product_type, action)
            VALUES (@Id, @Name, @CounterpartyId, 'Gas', 'Sell')
            """,
            new
            {
                Id = contractId,
                Name = $"TEST45.SG.{contractId:N}"[..40],
                CounterpartyId = counterpartyId,
            });
        return contractId;
    }

    private static async Task<CreatePhysicalDeliveryResponse> CreateDeliveryAsync(
        HttpClient client,
        Guid contractId,
        DateOnly supplyMonth)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/deliveries", new
        {
            contractId,
            bookType = "Sales",
            supplyMonth = supplyMonth.ToString("yyyy-MM-dd"),
            volumeNominatedMwh = 10m,
            volumeRealisedMwh = 9m,
            priceMechanism = "TTF",
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatePhysicalDeliveryResponse>())!;
    }

    private static async Task UpsertMarketPriceAndWaitAsync(
        HttpClient client,
        ConcurrentQueue<PushedEvent> events,
        DateOnly priceDate)
    {
        var aggregateId = priceDate.ToString("yyyy-MM-dd");
        using var response = await client.PutAsJsonAsync(
            $"/api/v1/market-prices/{aggregateId}",
            new { priceDate = aggregateId, ttfEurMwh = 31.5m, version = 0 });
        response.EnsureSuccessStatusCode();
        await WaitForAsync(events, item => item.AggregateId == aggregateId);
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
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] = Postgres.ConnectionString,
                    ["Jwt:Issuer"] = "Tradebook",
                    ["Jwt:Audience"] = "Tradebook",
                    ["Jwt:SigningKey"] = CustomWebApplicationFactory.JwtSigningKey,
                }));
        });

    private static async Task ResetWolverineAsync(
        WebApplicationFactory<Program> factory)
    {
        var runtime = factory.Services.GetRequiredService<IWolverineRuntime>();
        await runtime.Storage.Admin.ClearAllAsync();
    }

    private static HttpClient AuthenticatedClient(
        WebApplicationFactory<Program> factory,
        string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static HubConnection BuildHubConnection(
        WebApplicationFactory<Program> factory,
        string? token,
        out ConcurrentQueue<PushedEvent> events)
    {
        events = new ConcurrentQueue<PushedEvent>();
        var sink = events;
        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/dashboard"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                if (token is not null)
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                }
            })
            .AddMessagePackProtocol()
            .Build();
        hub.On<Guid, long, string, string, string, string>(
            "EntityChanged",
            (eventId, sequenceId, aggregateType, aggregateId, eventType, payloadJson) =>
                sink.Enqueue(new PushedEvent(
                    eventId,
                    sequenceId,
                    aggregateType,
                    aggregateId,
                    eventType,
                    payloadJson)));
        return hub;
    }

    private static async Task<(HubConnection Hub, ConcurrentQueue<PushedEvent> Events)>
        ConnectAsync(WebApplicationFactory<Program> factory, string token)
    {
        var hub = BuildHubConnection(factory, token, out var events);
        await hub.StartAsync();
        return (hub, events);
    }

    private static async Task<PushedEvent> WaitForAsync(
        ConcurrentQueue<PushedEvent> events,
        Func<PushedEvent, bool> predicate)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            var match = events.FirstOrDefault(predicate);
            if (match is not null)
            {
                return match;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException(
            $"No matching SignalR event arrived within 15 seconds ({events.Count} received).");
    }

    private static string CreateToken()
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(CustomWebApplicationFactory.JwtSigningKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "Tradebook",
            Audience = "Tradebook",
            Subject = new ClaimsIdentity([
                new Claim("sub", TestActorId.ToString()),
                new Claim(ClaimTypes.Role, "Admin"),
            ]),
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

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

    private sealed class InjectedCommandFailureException : Exception;

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
