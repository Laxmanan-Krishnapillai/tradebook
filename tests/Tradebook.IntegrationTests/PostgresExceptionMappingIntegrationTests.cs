using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Tradebook.IntegrationTests.Fixtures;

namespace Tradebook.IntegrationTests;

public sealed class PostgresExceptionMappingIntegrationTests(PostgresTestFixture postgres)
    : PostgresDatabaseTestBase(postgres)
{
    [Fact]
    public async Task DuplicateDomainCreateReturnsSafeConflictResponse()
    {
        var contractId = await SeedContractAsync();
        await using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var request = new { contractId, supplyMonth = "2026-01-01" };

        using var created = await client.PostAsJsonAsync("/api/v1/capacity-bookings", request);
        using var duplicate = await client.PostAsJsonAsync("/api/v1/capacity-bookings", request);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("application/problem+json", duplicate.Content.Headers.ContentType?.MediaType);
        var body = await duplicate.Content.ReadAsStringAsync();
        Assert.DoesNotContain("uk_capacity", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("duplicate key", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PartialDateUpdateAgainstStoredCounterpartReturnsBadRequestAndRollsBack()
    {
        var contractId = await SeedContractAsync();
        await using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        using var created = await client.PostAsJsonAsync(
            "/api/v1/capacity-bookings",
            new
            {
                contractId,
                supplyMonth = "2026-02-01",
                startDay = "2026-02-10",
                endDay = "2026-02-20",
            }
        );
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdBody = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var capacityBookingId = createdBody.RootElement.GetProperty("capacityBookingId").GetGuid();
        var version = createdBody.RootElement.GetProperty("version").GetInt64();

        using var invalidUpdate = await client.PutAsJsonAsync(
            $"/api/v1/capacity-bookings/{capacityBookingId}",
            new
            {
                capacityBookingId,
                endDay = "2026-02-05",
                version,
            }
        );

        Assert.Equal(HttpStatusCode.BadRequest, invalidUpdate.StatusCode);
        Assert.Equal(
            "application/problem+json",
            invalidUpdate.Content.Headers.ContentType?.MediaType
        );
        var errorBody = await invalidUpdate.Content.ReadAsStringAsync();
        Assert.DoesNotContain(
            "ck_capacity_delivery_dates",
            errorBody,
            StringComparison.OrdinalIgnoreCase
        );

        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        var persisted = await connection.QuerySingleAsync<(DateOnly EndDay, long Version)>(
            "SELECT end_day AS EndDay, version AS Version FROM capacity_bookings WHERE id = @Id",
            new { Id = capacityBookingId }
        );
        Assert.Equal(new DateOnly(2026, 2, 20), persisted.EndDay);
        Assert.Equal(version, persisted.Version);
        Assert.Equal(
            1,
            await WaitForRealtimeEventCountAsync(connection, capacityBookingId.ToString())
        );
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

    private static HttpClient AuthenticatedClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            Token(Guid.NewGuid())
        );
        return client;
    }

    private static string Token(Guid actorId)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "Tradebook",
            Audience = "Tradebook",
            Subject = new ClaimsIdentity([
                new Claim("oid", actorId.ToString()),
                new Claim("role", "Trader"),
            ]),
            Expires = TimeProvider.System.GetUtcNow().UtcDateTime.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(CustomWebApplicationFactory.JwtSigningKey)
                ),
                SecurityAlgorithms.HmacSha256
            ),
        };
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
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
                    Name = $"Constraint Counterparty {counterpartyId:N}",
                    Shorthand = $"CE{counterpartyId:N}"[..20],
                }
            )
            .ConfigureAwait(false);
        var contractId = Guid.NewGuid();
        await connection
            .ExecuteAsync(
                """
                INSERT INTO contracts
                    (id, contract_name, counterparty_id, product_type, action, subsidy_status)
                VALUES
                    (@Id, @Name, @CounterpartyId, 'Gas', 'Sell', 'SUB')
                """,
                new
                {
                    Id = contractId,
                    Name = $"ERR45.SG.{contractId:N}.NOQS",
                    CounterpartyId = counterpartyId,
                }
            )
            .ConfigureAwait(false);
        return contractId;
    }

    private static async Task<int> WaitForRealtimeEventCountAsync(
        NpgsqlConnection connection,
        string aggregateId
    )
    {
        var deadline = TimeProvider.System.GetUtcNow().UtcDateTime + TimeSpan.FromSeconds(15);
        while (TimeProvider.System.GetUtcNow().UtcDateTime < deadline)
        {
            var count = await connection
                .ExecuteScalarAsync<int>(
                    """
                    SELECT COUNT(*)
                    FROM realtime_event_log
                    WHERE aggregate_type = 'CapacityBooking' AND aggregate_id = @AggregateId
                    """,
                    new { AggregateId = aggregateId }
                )
                .ConfigureAwait(false);
            if (count > 0)
            {
                return count;
            }

            await Task.Delay(50).ConfigureAwait(false);
        }

        return 0;
    }
}
