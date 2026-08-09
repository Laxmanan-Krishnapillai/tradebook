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
    public async Task ReturnsOrderedPagesAndTheLatestSequenceFromRealPostgres()
    {
        await ResetOutboxAsync();
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
    public async Task CatchUpRequiresAJwt()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/events?afterSequence=0&limit=10");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CatchUpExcludesOtherActorsPrivateDashboardEvents()
    {
        await ResetOutboxAsync();
        var actorId = Guid.NewGuid();
        var otherActorId = Guid.NewGuid();
        await using (var connection = new NpgsqlConnection(Postgres.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO outbox_events (aggregate_type, aggregate_id, event_type, payload)
                VALUES
                    ('WorkspaceDashboard', gen_random_uuid()::text, 'Updated', jsonb_build_object('actorId', @otherActorId::text)),
                    ('WorkspaceDashboard', gen_random_uuid()::text, 'Updated', jsonb_build_object('actorId', @actorId::text)),
                    ('MarketPrice', '2026-08-08', 'Updated', '{}'::jsonb);
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
        Assert.Contains(
            response.Events,
            item =>
                string.Equals(item.AggregateType, "WorkspaceDashboard", StringComparison.Ordinal)
        );
        Assert.Contains(
            response.Events,
            item => string.Equals(item.AggregateType, "MarketPrice", StringComparison.Ordinal)
        );
        Assert.Equal(3, response.LatestSequence);
    }

    [Theory]
    [InlineData("afterSequence=-1&limit=10")]
    [InlineData("afterSequence=0&limit=0")]
    [InlineData("afterSequence=0&limit=501")]
    public async Task InvalidCursorParametersAreRejected(string query)
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
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["Database:ConnectionString"] = Postgres.ConnectionString,
                            ["Entra:TenantId"] = "11111111-1111-1111-1111-111111111111",
                            ["Entra:ClientId"] = "22222222-2222-2222-2222-222222222222",
                        }
                    )
            );
        });

    private async Task ResetOutboxAsync()
    {
        await using var connection = new NpgsqlConnection(Postgres.ConnectionString).ConfigureAwait(
            false
        );
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            "TRUNCATE TABLE outbox_events RESTART IDENTITY",
            connection
        );
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private async Task InsertEventsAsync(int count)
    {
        await using var connection = new NpgsqlConnection(Postgres.ConnectionString).ConfigureAwait(
            false
        );
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO outbox_events (aggregate_type, aggregate_id, event_type, payload)
            SELECT CASE WHEN value = 1 THEN 'MarketPrice' ELSE 'PhysicalDelivery' END,
                   CASE WHEN value = 1 THEN '2026-08-07' ELSE gen_random_uuid()::text END,
                   'Created',
                   jsonb_build_object('ordinal', value)
            FROM generate_series(1, @count) AS value;
            """,
            connection
        );
        command.Parameters.AddWithValue("count", count);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
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
            Expires = TimeProvider.System.GetUtcNow().UtcDateTime.AddMinutes(5),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };
        return new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityTokenHandler().CreateToken(descriptor)
        );
    }

    private sealed record CatchUpEvent(long SequenceId, string AggregateType, string AggregateId);

    private sealed record CatchUpResponse(IReadOnlyList<CatchUpEvent> Events, long LatestSequence);
}
