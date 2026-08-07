using System.Collections.Concurrent;
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
using Tradebook.IntegrationTests.Fixtures;
using Tradebook.Infrastructure.Outbox;

namespace Tradebook.IntegrationTests.RealTime;

[Trait("Category", "RealTime")]
public sealed class OutboxDispatchTests(PostgresTestFixture postgres) : PostgresDatabaseTestBase(postgres)
{
    private static readonly Guid TestActorId = Guid.Parse("3c7280d0-ab9e-48d1-87e9-848244111320");
    private sealed record PushedEvent(Guid EventId, long SequenceId, string AggregateType,
        string AggregateId, string EventType, string PayloadJson);

    private sealed class ClientEventSink
    {
        private readonly ConcurrentDictionary<Guid, byte> _seen = new();

        public ConcurrentQueue<PushedEvent> Received { get; } = new();
        public ConcurrentQueue<PushedEvent> Applied { get; } = new();

        public void OnEvent(PushedEvent pushedEvent)
        {
            Received.Enqueue(pushedEvent);
            if (_seen.TryAdd(pushedEvent.EventId, 0))
            {
                Applied.Enqueue(pushedEvent);
            }
        }
    }

    private sealed record CreatedDelivery(Guid DeliveryId);

    [Fact] // Task 03 §6 T1
    public async Task Posting_a_delivery_pushes_EntityChanged_and_marks_the_outbox_row_processed()
    {
        await using var factory = CreateFactory();
        var contractId = await SeedContractAsync();
        var (hub, events) = await ConnectAsync(factory, CreateToken());
        await using (hub)
        {
            await hub.InvokeAsync("Subscribe", "entity:PhysicalDelivery");

            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());
            var response = await client.PostAsJsonAsync("/api/v1/deliveries", new
            {
                contractId,
                bookType = "Sales",
                supplyMonth = "2026-02-01",
                volumeNominatedMwh = 10m,
                volumeRealisedMwh = 9m,
                priceMechanism = "TTF"
            });
            response.EnsureSuccessStatusCode();
            var created = await response.Content.ReadFromJsonAsync<CreatedDelivery>();

            var pushed = await WaitForAsync(events.Received,
                e => e.AggregateId == created!.DeliveryId.ToString() && e.EventType == "Created",
                TimeSpan.FromSeconds(5));
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

    [Fact]
    public async Task Subscribing_to_another_actors_dashboard_group_is_rejected()
    {
        await using var factory = CreateFactory();
        var (hub, _) = await ConnectAsync(factory, CreateToken());
        await using (hub)
        {
            await Assert.ThrowsAsync<HubException>(() =>
                hub.InvokeAsync("Subscribe", $"dashboard:{Guid.NewGuid()}"));
        }
    }

    [Fact] // Task 03 §6 T4 — at-least-once redelivery with client-side dedup
    public async Task A_failed_mark_processed_batch_is_redispatched_and_deduplicated_by_eventId()
    {
        await using var factory = CreateFactory(
            config: new Dictionary<string, string?> { ["Outbox:ErrorBackoffSeconds"] = "1" },
            services: services => services.AddSingleton<IOutboxDispatchObserver>(new FailOnceObserver()));
        var (hub, events) = await ConnectAsync(factory, CreateToken());
        await using (hub)
        {
            await hub.InvokeAsync("Subscribe", "entity:PhysicalDelivery");
            var aggregateId = await InsertOutboxEventAsync("PhysicalDelivery");

            var first = await WaitForAsync(events.Received, e => e.AggregateId == aggregateId, TimeSpan.FromSeconds(5));
            var second = await WaitForAsync(events.Received,
                e => e.AggregateId == aggregateId, TimeSpan.FromSeconds(10), minimumMatches: 2);
            Assert.Equal(first.EventId, second.EventId);
            Assert.True(await WaitForProcessedAsync(first.EventId, TimeSpan.FromSeconds(10)),
                "outbox row was never marked processed after the injected failure");

            Assert.Single(events.Applied, e => e.AggregateId == aggregateId);
        }
    }

    [Fact] // Task 03 §6 T5 — every whitelisted aggregate type reaches its group
    public async Task Every_mutation_producer_emits_the_registered_aggregate_type_to_its_group()
    {
        await using var factory = CreateFactory();
        var (hub, events) = await ConnectAsync(factory, CreateToken());
        await using (hub)
        {
            foreach (var aggregateType in OutboxAggregateTypes.All)
            {
                if (aggregateType == OutboxAggregateTypes.WorkspaceDashboard) continue;
                await hub.InvokeAsync("Subscribe", $"entity:{aggregateType}");
            }
            await hub.InvokeAsync("Subscribe", $"dashboard:{TestActorId}");

            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", CreateToken());
            var counterpartyId = await SeedCounterpartyAsync();
            var contractId = await PostAndReadGuidAsync(
                client,
                "/api/v1/contracts",
                new
                {
                    contractName = "TST45.SG.2601.NOQS",
                    counterpartyId,
                    productType = "Gas",
                    action = "Sell",
                    contractType = "External"
                },
                "contractId");
            var deliveryId = await PostAndReadGuidAsync(
                client,
                "/api/v1/deliveries",
                new { contractId, bookType = "Sales", supplyMonth = "2026-01-01" },
                "deliveryId");
            var capacityBookingId = await PostAndReadGuidAsync(
                client,
                "/api/v1/capacity-bookings",
                new { contractId, supplyMonth = "2026-02-01" },
                "capacityBookingId");
            var transferId = await PostAndReadGuidAsync(
                client,
                "/api/v1/transfers",
                new { contractId, supplyMonth = "2026-03-01" },
                "transferId");
            var bioticketId = await PostAndReadGuidAsync(
                client,
                "/api/v1/biotickets",
                new { contractId, bookType = "Sales", contractMonth = "2026-04-01" },
                "bioticketId");
            var certificateId = await PostAndReadGuidAsync(
                client,
                "/api/v1/goo-certificates",
                new { transactionName = $"dispatch-{Guid.NewGuid():N}", producerContractId = contractId },
                "gooCertificateTransactionId");

            const string priceDate = "2026-05-01";
            using (var response = await client.PutAsJsonAsync(
                       $"/api/v1/market-prices/{priceDate}",
                       new { priceDate, ttfEurMwh = 31.5m, version = 0 }))
            {
                response.EnsureSuccessStatusCode();
            }

            var taxTariffId = await PostAndReadGuidAsync(
                client,
                "/api/v1/tax-tariffs",
                new
                {
                    contractId,
                    periodStart = "2026-06-01",
                    periodEnd = "2026-06-30",
                    currency = "SEK"
                },
                "taxTariffId");
            var hedgeId = await PostAndReadGuidAsync(
                client,
                "/api/v1/hedges",
                new
                {
                    contractId,
                    month = "2026-07-01",
                    hedgeAmountMwh = 10m,
                    hedgePriceEurMwh = 30m
                },
                "hedgeId");

            var dashboardId = Guid.NewGuid();
            using (var response = await client.PutAsJsonAsync(
                       $"/api/v1/dashboards/{dashboardId}",
                       new
                       {
                           dashboardId,
                           version = 0,
                           layout = new
                           {
                               dashboardId,
                               title = "Realtime producer coverage",
                               version = 0,
                               theme = "SYSTEM",
                               refreshRateMs = 30_000,
                               gridLayout = new
                               {
                                   columns = 12,
                                   rowHeight = 30,
                                   items = Array.Empty<object>()
                               },
                               widgets = Array.Empty<object>()
                           }
                       }))
            {
                response.EnsureSuccessStatusCode();
            }

            var expected = new Dictionary<string, string>
            {
                [OutboxAggregateTypes.Contract] = contractId.ToString(),
                [OutboxAggregateTypes.PhysicalDelivery] = deliveryId.ToString(),
                [OutboxAggregateTypes.CapacityBooking] = capacityBookingId.ToString(),
                [OutboxAggregateTypes.Transfer] = transferId.ToString(),
                [OutboxAggregateTypes.BioticketDelivery] = bioticketId.ToString(),
                [OutboxAggregateTypes.GooCertificateTransaction] = certificateId.ToString(),
                [OutboxAggregateTypes.MarketPrice] = priceDate,
                [OutboxAggregateTypes.TaxTariff] = taxTariffId.ToString(),
                [OutboxAggregateTypes.Hedge] = hedgeId.ToString(),
                [OutboxAggregateTypes.WorkspaceDashboard] = dashboardId.ToString()
            };
            Assert.Equal(OutboxAggregateTypes.All.Count, expected.Count);

            foreach (var (aggregateType, aggregateId) in expected)
            {
                var pushed = await WaitForAsync(events.Received,
                    e => e.AggregateId == aggregateId, TimeSpan.FromSeconds(10));
                Assert.Equal(aggregateType, pushed.AggregateType);
                Assert.Equal("Created", pushed.EventType);
            }
        }
    }

    [Fact] // Task 03 §6 T7 — NOTIFY wake-up, not the fallback poll
    public async Task A_new_outbox_row_is_delivered_via_NOTIFY_without_waiting_for_the_fallback_poll()
    {
        await using var factory = CreateFactory(
            config: new Dictionary<string, string?> { ["Outbox:FallbackPollSeconds"] = "30" });
        var (hub, events) = await ConnectAsync(factory, CreateToken());
        await using (hub)
        {
            await hub.InvokeAsync("Subscribe", "entity:PhysicalDelivery");
            await Task.Delay(TimeSpan.FromSeconds(2)); // let the start-up drain finish so only LISTEN can wake it

            var start = DateTime.UtcNow;
            var aggregateId = await InsertOutboxEventAsync("PhysicalDelivery");
            await WaitForAsync(events.Received, e => e.AggregateId == aggregateId, TimeSpan.FromSeconds(5));
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
                var settings = new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] = Postgres.ConnectionString,
                    ["Jwt:Issuer"] = "Tradebook",
                    ["Jwt:Audience"] = "Tradebook",
                    ["Jwt:SigningKey"] = CustomWebApplicationFactory.JwtSigningKey
                };
                foreach (var pair in config ?? []) settings[pair.Key] = pair.Value;
                configuration.AddInMemoryCollection(settings);
            });
            if (services is not null) builder.ConfigureServices(services);
        });

    private static HubConnection BuildHubConnection(WebApplicationFactory<Program> factory, string? token,
        out ClientEventSink events)
    {
        var sink = new ClientEventSink();
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
        hub.On<Guid, long, string, string, string, string>("EntityChanged",
            (eventId, sequenceId, aggregateType, aggregateId, eventType, payloadJson) =>
                sink.OnEvent(new PushedEvent(
                    eventId,
                    sequenceId,
                    aggregateType,
                    aggregateId,
                    eventType,
                    payloadJson)));
        events = sink;
        return hub;
    }

    private static async Task<(HubConnection Hub, ClientEventSink Events)> ConnectAsync(
        WebApplicationFactory<Program> factory, string token)
    {
        var hub = BuildHubConnection(factory, token, out var events);
        await hub.StartAsync();
        return (hub, events);
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
        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
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

    private async Task<string> InsertOutboxEventAsync(string aggregateType, string? aggregateId = null)
    {
        aggregateId ??= Guid.NewGuid().ToString();
        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await connection.ExecuteAsync("""
            INSERT INTO outbox_events (aggregate_type, aggregate_id, event_type, payload)
            VALUES (@AggregateType, @AggregateId, 'Created', '{"source":"OutboxDispatchTests"}'::jsonb)
            """, new { AggregateType = aggregateType, AggregateId = aggregateId });
        return aggregateId;
    }

    private async Task<Guid> SeedContractAsync()
    {
        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
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

    private async Task<Guid> SeedCounterpartyAsync()
    {
        var counterpartyId = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await connection.ExecuteAsync("""
            INSERT INTO counterparties
                (id, name, shorthand, segment, country_code, country_dial_code)
            VALUES
                (@Id, @Name, @Shorthand, 'Traders', 'DK', 45)
            """, new
            {
                Id = counterpartyId,
                Name = $"Counterparty-{counterpartyId:N}",
                Shorthand = $"CP{counterpartyId:N}"[..20]
            });
        return counterpartyId;
    }

    private static async Task<Guid> PostAndReadGuidAsync(
        HttpClient client,
        string route,
        object request,
        string propertyName)
    {
        using var response = await client.PostAsJsonAsync(route, request);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty(propertyName).GetGuid();
    }

    private static string CreateToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(CustomWebApplicationFactory.JwtSigningKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "Tradebook",
            Audience = "Tradebook",
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", TestActorId.ToString()),
                new Claim(ClaimTypes.Role, "Admin")
            }),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityTokenHandler().CreateToken(descriptor));
    }
}
