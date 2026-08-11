using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Dapper;
using Npgsql;
using Tradebook.Core.DTOs;
using Tradebook.IntegrationTests.Fixtures;

namespace Tradebook.IntegrationTests;

public sealed class ActivityEndpointIntegrationTests(CustomWebApplicationFactory factory)
    : DatabaseTestBase(factory)
{
    [Fact]
    public async Task ActivityRequiresAuthenticationAndReturnsEveryAuditPatch()
    {
        const string entityId = "delivery-activity-test";
        await using (var connection = new NpgsqlConnection(Factory.ConnectionString))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync(
                """
                INSERT INTO audit_log
                    (audit_id, entity_name, entity_id, actor_id, operation, system_time,
                     valid_time, diff_patch, commit_hash)
                VALUES
                    (@SystemId, 'physical_deliveries', @EntityId, '00000000-0000-0000-0000-000000000000', 'INSERT',
                     tstzrange('2026-01-01T00:00:00Z', '2026-01-02T00:00:00Z', '[)'),
                     tstzrange('2026-01-01T00:00:00Z', NULL, '[)'),
                     '[]'::jsonb, @SystemHash),
                    (@FirstId, 'physical_deliveries', @EntityId, @ActorId, 'UPDATE',
                     tstzrange('2026-01-02T00:00:00Z', '2026-01-03T00:00:00Z', '[)'),
                     tstzrange('2026-01-01T00:00:00Z', NULL, '[)'),
                     '[{"op":"replace","path":"/status","value":"Completed"}]'::jsonb, @FirstHash),
                    (@SecondId, 'physical_deliveries', @EntityId, @ActorId, 'UPDATE',
                     tstzrange('2026-01-03T00:00:00Z', NULL, '[)'),
                     tstzrange('2026-01-01T00:00:00Z', NULL, '[)'),
                     '[{"op":"replace","path":"/comments","value":"Checked"}]'::jsonb, @SecondHash)
                """,
                new
                {
                    FirstId = Guid.NewGuid(),
                    SecondId = Guid.NewGuid(),
                    SystemId = Guid.NewGuid(),
                    EntityId = entityId,
                    ActorId = Guid.NewGuid(),
                    SystemHash = new string('0', 64),
                    FirstHash = new string('a', 64),
                    SecondHash = new string('b', 64),
                }
            );
        }

        var anonymous = await Client.GetAsync($"/api/v1/activity/physical_deliveries/{entityId}");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestToken(Guid.NewGuid())
        );
        var activity = await Client.GetFromJsonAsync<GetActivityResponse>(
            $"/api/v1/activity/physical_deliveries/{entityId}"
        );

        Assert.NotNull(activity);
        Assert.Equal(3, activity.Items.Count);
        Assert.Equal(
            "/comments",
            activity.Items[0].Changes.EnumerateArray().First().GetProperty("path").GetString()
        );
        Assert.Equal(
            "/status",
            activity.Items[1].Changes.EnumerateArray().First().GetProperty("path").GetString()
        );
        Assert.Null(activity.Items[2].ActorId);
    }

    private static string TestToken(Guid actorId) =>
        new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(
                claims: [new Claim("oid", actorId.ToString()), new Claim("role", "Trader")],
                expires: TimeProvider.System.GetUtcNow().UtcDateTime.AddMinutes(5)
            )
        );
}
