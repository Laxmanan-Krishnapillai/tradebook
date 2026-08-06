using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace Tradebook.IntegrationTests.RealTime;

[Trait("Category", "RealTime")]
public sealed class CatchUpEndpointTests(PostgresTestFixture postgres) : IClassFixture<PostgresTestFixture>
{
    [Fact]
    public async Task Returns_ordered_pages_and_the_latest_sequence_from_real_postgres()
    {
        await InsertEventsAsync(25);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());

        var sequences = new List<long>();
        for (var afterSequence = 0L; ;)
        {
            var response = await client.GetFromJsonAsync<CatchUpResponse>($"/api/v1/events?afterSequence={afterSequence}&limit=10");
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
    }

    private WebApplicationFactory<Program> CreateFactory() => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = postgres.ConnectionString
            })));

    private async Task InsertEventsAsync(int count)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            INSERT INTO outbox_events (aggregate_type, aggregate_id, event_type, payload)
            SELECT 'PhysicalDelivery', gen_random_uuid()::text, 'Created', jsonb_build_object('ordinal', value)
            FROM generate_series(1, @count) AS value;
            """, connection);
        command.Parameters.AddWithValue("count", count);
        await command.ExecuteNonQueryAsync();
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

    private sealed record CatchUpEvent(long SequenceId);
    private sealed record CatchUpResponse(IReadOnlyList<CatchUpEvent> Events, long LatestSequence);
}
