using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Tradebook.IntegrationTests.Fixtures;

namespace Tradebook.IntegrationTests.RealTime;

[Trait("Category", "RealTime")]
public sealed class CatchUpEndpointTests(PostgresTestFixture postgres)
    : PostgresDatabaseTestBase(postgres)
{
    [Fact]
    public async Task Returns_ordered_pages_and_the_latest_sequence_from_real_postgres()
    {
        await ResetRealtimeEventLogAsync();
        await InsertEventsAsync(25);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken()
        );

        var sequences = new List<long>();
        for (var afterSequence = 0L; ; )
        {
            var response = await client.GetFromJsonAsync<CatchUpResponse>(
                $"/api/v1/events?afterSequence={afterSequence}&limit=10"
            );
            Assert.NotNull(response);
            sequences.AddRange(response.Events.Select(static e => e.SequenceId));
            if (response.Events.Count < 10)
            {
                Assert.Equal(25, response.LatestSequence);
                break;
            }

            afterSequence = response.Events[^1].SequenceId;
        }

        Assert.Equal(Enumerable.Range(1, 25).Select(static value => (long)value), sequences);

        var firstPage = await client.GetFromJsonAsync<CatchUpResponse>(
            "/api/v1/events?afterSequence=0&limit=1"
        );
        Assert.NotNull(firstPage);
        Assert.Equal("MarketPrice", firstPage.Events[0].AggregateType);
        Assert.Equal("2026-08-07", firstPage.Events[0].AggregateId);
    }

    [Fact]
    public async Task Catch_up_requires_a_jwt()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/events?afterSequence=0&limit=10");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Catch_up_excludes_other_actors_private_dashboard_events()
    {
        await ResetRealtimeEventLogAsync();
        var actorId = Guid.NewGuid();
        var otherActorId = Guid.NewGuid();
        await using (var connection = new NpgsqlConnection(Postgres.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO realtime_event_log
                    (event_id, group_name, aggregate_type, aggregate_id, event_type, payload)
                VALUES
                    (gen_random_uuid(), 'dashboard:' || @otherActorId::text, 'WorkspaceDashboard', gen_random_uuid()::text, 'Updated', jsonb_build_object('actorId', @otherActorId::text)),
                    (gen_random_uuid(), 'dashboard:' || @actorId::text, 'WorkspaceDashboard', gen_random_uuid()::text, 'Updated', jsonb_build_object('actorId', @actorId::text)),
                    (gen_random_uuid(), 'entity:MarketPrice', 'MarketPrice', '2026-08-08', 'Updated', '{}'::jsonb);
                """,
                connection
            );
            command.Parameters.AddWithValue("actorId", actorId);
            command.Parameters.AddWithValue("otherActorId", otherActorId);
            await command.ExecuteNonQueryAsync();
        }

        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(actorId)
        );
        var response = await client.GetFromJsonAsync<CatchUpResponse>(
            "/api/v1/events?afterSequence=0&limit=10"
        );

        Assert.NotNull(response);
        Assert.Equal(2, response.Events.Count);
        Assert.Contains(response.Events, item => item.AggregateType == "WorkspaceDashboard");
        Assert.Contains(response.Events, item => item.AggregateType == "MarketPrice");
        Assert.Equal(3, response.LatestSequence);
    }

    [Theory]
    [InlineData("afterSequence=-1&limit=10")]
    [InlineData("afterSequence=0&limit=0")]
    [InlineData("afterSequence=0&limit=501")]
    public async Task Invalid_cursor_parameters_are_rejected(string query)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken()
        );

        var response = await client.GetAsync($"/api/v1/events?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Database:ConnectionString", Postgres.ConnectionString);
            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["Database:ConnectionString"] = Postgres.ConnectionString,
                            ["Jwt:Issuer"] = "Tradebook",
                            ["Jwt:Audience"] = "Tradebook",
                            ["Jwt:SigningKey"] = CustomWebApplicationFactory.JwtSigningKey,
                        }
                    )
            );
        });

    private async Task ResetRealtimeEventLogAsync()
    {
        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "TRUNCATE TABLE realtime_event_log RESTART IDENTITY",
            connection
        );
        await command.ExecuteNonQueryAsync();
    }

    private async Task InsertEventsAsync(int count)
    {
        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO realtime_event_log
                (event_id, group_name, aggregate_type, aggregate_id, event_type, payload)
            SELECT gen_random_uuid(),
                   CASE WHEN value = 1 THEN 'entity:MarketPrice' ELSE 'entity:PhysicalDelivery' END,
                   CASE WHEN value = 1 THEN 'MarketPrice' ELSE 'PhysicalDelivery' END,
                   CASE WHEN value = 1 THEN '2026-08-07' ELSE gen_random_uuid()::text END,
                   'Created',
                   jsonb_build_object('ordinal', value)
            FROM generate_series(1, @count) AS value;
            """,
            connection
        );
        command.Parameters.AddWithValue("count", count);
        await command.ExecuteNonQueryAsync();
    }

    private static string CreateToken(Guid? actorId = null)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(CustomWebApplicationFactory.JwtSigningKey)
        );
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "Tradebook",
            Audience = "Tradebook",
            Subject = new ClaimsIdentity(
                new[]
                {
                    new Claim("sub", (actorId ?? Guid.NewGuid()).ToString()),
                    new Claim(ClaimTypes.Role, "Trader"),
                }
            ),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };
        return new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityTokenHandler().CreateToken(descriptor)
        );
    }

    private sealed record CatchUpEvent(long SequenceId, string AggregateType, string AggregateId);

    private sealed record CatchUpResponse(IReadOnlyList<CatchUpEvent> Events, long LatestSequence);
}
