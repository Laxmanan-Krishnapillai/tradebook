using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
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
using Tradebook.Infrastructure.Outbox;

namespace Tradebook.IntegrationTests.RealTime;

[Trait("Category", "RealTime")]
public sealed class OutboxDispatchTests(PostgresTestFixture postgres) : IClassFixture<PostgresTestFixture>
{
    private sealed record PushedEvent(Guid EventId, long SequenceId, string AggregateType,
        Guid AggregateId, string EventType, string PayloadJson);

    private sealed record CreatedDelivery(Guid DeliveryId);

    [Fact] // Task 03 §6 T1
    public async Task Posting_a_delivery_pushes_EntityChanged_and_marks_the_outbox_row_processed()
    {
        await using var factory = CreateFactory();
        var contractId = await SeedContractAsync();
        var (hub, received) = await ConnectAsync(factory, CreateToken());
        await using (hub)
        {
            await hub.InvokeAsync("Subscribe", "entity:PhysicalDelivery");

            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());
            var response = await client.PostAsJsonAsync("/api/v1/deliveries", new
            {
                contractId,
                contractInstanceId = $"TEST45.SG.2602.NOQS-{Guid.NewGuid():N}"[..30],
                bookType = "Sales",
                supplyMonth = "2026-02-01",
                volumeNominatedMwh = 10m,
                volumeRealisedMwh = 9m,
                priceMechanism = "TTF"
            });
            response.EnsureSuccessStatusCode();
            var created = await response.Content.ReadFromJsonAsync<CreatedDelivery>();

            var pushed = await WaitForAsync(received,
                e => e.AggregateId == created!.DeliveryId && e.EventType == "Created", TimeSpan.FromSeconds(5));
            Assert.Equal("PhysicalDelivery", pushed.AggregateType);
            Assert.True(await WaitForProcessedAsync(pushed.EventId, TimeSpan.FromSeconds(5)),
                "outbox row was never marked processed");
        }
    }

    [Fact] // Task 03 §6 T2
    public async Task Unauthenticated_hub_connections_are_rejected()
    {
        await using var factory = CreateFactory();
        var hub = BuildHubConnection(factory, token: null, out _);
        await using (hub)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => hub.StartAsync());
        }
    }

    [Fact] // Task 03 §6 T3
    public async Task Subscribing_to_an_unknown_group_surfaces_a_HubException()
    {
        await using var factory = CreateFactory();
        var (hub, _) = await ConnectAsync(factory, CreateToken());
        await using (hub)
        {
            await Assert.ThrowsAsync<HubException>(() => hub.InvokeAsync("Subscribe", "entity:Nope"));
        }
    }

    [Fact] // Task 03 §6 T4 — at-least-once redelivery with client-side dedup
    public async Task A_failed_mark_processed_batch_is_redispatched_and_deduplicated_by_eventId()
    {
        await using var factory = CreateFactory(
            config: new Dictionary<string, string?> { ["Outbox:ErrorBackoffSeconds"] = "1" },
            services: services => services.AddSingleton<IOutboxDispatchObserver>(new FailOnceObserver()));
        var (hub, received) = await ConnectAsync(factory, CreateToken());
        await using (hub)
        {
            await hub.InvokeAsync("Subscribe", "entity:PhysicalDelivery");
            var aggregateId = await InsertOutboxEventAsync("PhysicalDelivery");

            var first = await WaitForAsync(received, e => e.AggregateId == aggregateId, TimeSpan.FromSeconds(5));
            var second = await WaitForAsync(received,
                e => e.AggregateId == aggregateId, TimeSpan.FromSeconds(10), minimumMatches: 2);
            Assert.Equal(first.EventId, second.EventId);
            Assert.True(await WaitForProcessedAsync(first.EventId, TimeSpan.FromSeconds(10)),
                "outbox row was never marked processed after the injected failure");

            // Client dedup contract: LRU by eventId keeps exactly one application.
            var applied = received.Where(e => e.AggregateId == aggregateId).Select(e => e.EventId).Distinct().Count();
            Assert.Equal(1, applied);
        }
    }

    [Fact] // Task 03 §6 T5 — every whitelisted aggregate type reaches its group
    public async Task Every_whitelisted_aggregate_type_is_delivered_to_its_entity_group()
    {
        string[] aggregateTypes =
            ["PhysicalDelivery", "Contract", "CapacityBooking", "GooCertificateTransaction", "MarketPrice", "Hedge"];
        await using var factory = CreateFactory();
        var (hub, received) = await ConnectAsync(factory, CreateToken());
        await using (hub)
        {
            foreach (var aggregateType in aggregateTypes)
            {
                await hub.InvokeAsync("Subscribe", $"entity:{aggregateType}");
            }

            var inserted = new Dictionary<string, Guid>();
            foreach (var aggregateType in aggregateTypes)
            {
                inserted[aggregateType] = await InsertOutboxEventAsync(aggregateType);
            }

            foreach (var (aggregateType, aggregateId) in inserted)
            {
                var pushed = await WaitForAsync(received,
                    e => e.AggregateId == aggregateId, TimeSpan.FromSeconds(10));
                Assert.Equal(aggregateType, pushed.AggregateType);
            }
        }
    }

    [Fact] // Task 03 §6 T7 — NOTIFY wake-up, not the fallback poll
    public async Task A_new_outbox_row_is_delivered_via_NOTIFY_without_waiting_for_the_fallback_poll()
    {
        await using var factory = CreateFactory(
            config: new Dictionary<string, string?> { ["Outbox:FallbackPollSeconds"] = "30" });
        var (hub, received) = await ConnectAsync(factory, CreateToken());
        await using (hub)
        {
            await hub.InvokeAsync("Subscribe", "entity:PhysicalDelivery");
            await Task.Delay(TimeSpan.FromSeconds(2)); // let the start-up drain finish so only LISTEN can wake it

            var start = DateTime.UtcNow;
            var aggregateId = await InsertOutboxEventAsync("PhysicalDelivery");
            await WaitForAsync(received, e => e.AggregateId == aggregateId, TimeSpan.FromSeconds(5));
            // Recorded, not gated (D10):
            Console.WriteLine($"insert->push latency: {(DateTime.UtcNow - start).TotalMilliseconds:F0} ms");
        }
    }

    private sealed class FailOnceObserver : IOutboxDispatchObserver
    {
        private int _invocations;

        public Task BeforeMarkProcessedAsync(IReadOnlyList<OutboxEventRecord> batch, CancellationToken cancellationToken)
            => Interlocked.Exchange(ref _invocations, 1) == 0
                ? Task.FromException(new InvalidOperationException("Injected mark-processed failure (T4)"))
                : Task.CompletedTask;
    }

    private WebApplicationFactory<Program> CreateFactory(
        Dictionary<string, string?>? config = null, Action<IServiceCollection>? services = null)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var settings = new Dictionary<string, string?> { ["Database:ConnectionString"] = postgres.ConnectionString };
                foreach (var pair in config ?? []) settings[pair.Key] = pair.Value;
                configuration.AddInMemoryCollection(settings);
            });
            if (services is not null) builder.ConfigureServices(services);
        });

    private static HubConnection BuildHubConnection(WebApplicationFactory<Program> factory, string? token,
        out ConcurrentQueue<PushedEvent> received)
    {
        var queue = new ConcurrentQueue<PushedEvent>();
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
        hub.On<Guid, long, string, Guid, string, string>("EntityChanged",
            (eventId, sequenceId, aggregateType, aggregateId, eventType, payloadJson) =>
                queue.Enqueue(new PushedEvent(eventId, sequenceId, aggregateType, aggregateId, eventType, payloadJson)));
        received = queue;
        return hub;
    }

    private static async Task<(HubConnection Hub, ConcurrentQueue<PushedEvent> Received)> ConnectAsync(
        WebApplicationFactory<Program> factory, string token)
    {
        var hub = BuildHubConnection(factory, token, out var received);
        await hub.StartAsync();
        return (hub, received);
    }

    private static async Task<PushedEvent> WaitForAsync(ConcurrentQueue<PushedEvent> received,
        Func<PushedEvent, bool> predicate, TimeSpan timeout, int minimumMatches = 1)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var matches = received.Where(predicate).ToList();
            if (matches.Count >= minimumMatches)
            {
                return matches[^1];
            }

            await Task.Delay(50);
        }

        throw new TimeoutException(
            $"No SignalR EntityChanged message matching the predicate arrived within {timeout.TotalSeconds:F0}s " +
            $"(received {received.Count} total).");
    }

    private async Task<bool> WaitForProcessedAsync(Guid eventId, TimeSpan timeout)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var processed = await connection.ExecuteScalarAsync<bool>(
                "SELECT processed_at IS NOT NULL FROM outbox_events WHERE event_id = @EventId", new { EventId = eventId });
            if (processed)
            {
                return true;
            }

            await Task.Delay(100);
        }

        return false;
    }

    private async Task<Guid> InsertOutboxEventAsync(string aggregateType)
    {
        var aggregateId = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.ExecuteAsync("""
            INSERT INTO outbox_events (aggregate_type, aggregate_id, event_type, payload)
            VALUES (@AggregateType, @AggregateId, 'Created', '{"source":"OutboxDispatchTests"}'::jsonb)
            """, new { AggregateType = aggregateType, AggregateId = aggregateId.ToString() });
        return aggregateId;
    }

    private async Task<Guid> SeedContractAsync()
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        var counterpartyId = Guid.NewGuid();
        await connection.ExecuteAsync("INSERT INTO counterparties (id, name, shorthand) VALUES (@Id, @Name, @Shorthand)",
            new { Id = counterpartyId, Name = $"Counterparty-{counterpartyId}", Shorthand = $"CP{counterpartyId:N}"[..20] });
        var contractId = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO contracts (id, contract_name, counterparty_id, product_type, action) VALUES (@Id, @Name, @CounterpartyId, 'Gas', 'Sell')",
            new { Id = contractId, Name = $"TEST45.SG.{contractId:N}"[..40], CounterpartyId = counterpartyId });
        return contractId;
    }

    private static string CreateToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("development-only-signing-key-must-be-replaced"));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "Tradebook",
            Audience = "Tradebook",
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "Trader")
            }),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityTokenHandler().CreateToken(descriptor));
    }
}
