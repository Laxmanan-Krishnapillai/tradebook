using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Tradebook.IntegrationTests.Fixtures;

namespace Tradebook.IntegrationTests;

public sealed class AnalyticsQueryEndpointTests(CustomWebApplicationFactory factory)
    : DatabaseTestBase(factory)
{
    private static readonly string[] RevenueMeasures = ["revenue_eur"];
    private static readonly string[] BookTypeDimensions = ["book_type"];
    private static readonly string[] InvalidFilterValues = ["x"];

    [Fact]
    public async Task QueryRequiresAJwt()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/v1/analytics/query",
            new { modelName = "delivery_pnl_analytics", measures = RevenueMeasures }
        );
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnknownFilterMemberReturnsBadRequest()
    {
        Authenticate();
        var response = await Client.PostAsJsonAsync(
            "/api/v1/analytics/query",
            new
            {
                modelName = "delivery_pnl_analytics",
                measures = RevenueMeasures,
                filters = new[]
                {
                    new
                    {
                        member = "not_a_member",
                        @operator = "equals",
                        values = InvalidFilterValues,
                    },
                },
            }
        );
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "Unknown filter member",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task SeededDeliveryReturnsColumnsAndRows()
    {
        await SeedAsync();
        Authenticate();
        var response = await Client.PostAsJsonAsync(
            "/api/v1/analytics/query",
            new
            {
                modelName = "delivery_pnl_analytics",
                dimensions = BookTypeDimensions,
                measures = RevenueMeasures,
            }
        );
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("book_type", document.RootElement.GetProperty("columns")[0].GetString());
        Assert.Equal("revenue_eur", document.RootElement.GetProperty("columns")[1].GetString());
        Assert.Equal("Sales", document.RootElement.GetProperty("rows")[0][0].GetString());
        // D20: monetary decimals serialize as JSON strings (MoneyJsonConverter).
        var revenue = document.RootElement.GetProperty("rows")[0][1].GetString();
        Assert.Equal(
            1234.56m,
            decimal.Parse(revenue!, NumberStyles.Number, CultureInfo.InvariantCulture)
        );
    }

    private void Authenticate() =>
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken()
        );

    private static string CreateToken()
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(CustomWebApplicationFactory.JwtSigningKey)
        );
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new System.Security.Claims.ClaimsIdentity([
                new("sub", Guid.NewGuid().ToString()),
                new("role", "Trader"),
            ]),
            Issuer = "Tradebook",
            Audience = "Tradebook",
            Expires = TimeProvider.System.GetUtcNow().UtcDateTime.AddMinutes(5),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };
        return new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityTokenHandler().CreateToken(descriptor)
        );
    }

    private async Task SeedAsync()
    {
        var connection = new NpgsqlConnection(Factory.ConnectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            const string sql = """
                INSERT INTO companies (id, shorthand, name) VALUES ('00000000-0000-0000-0000-000000000001', 'BGEM', 'BioGem');
                INSERT INTO counterparties (id, name, shorthand) VALUES ('00000000-0000-0000-0000-000000000002', 'Counterparty', 'CP');
                INSERT INTO contracts (id, contract_name, counterparty_id, product_type, action) VALUES ('00000000-0000-0000-0000-000000000003', 'CP45.SC.2601.ETS', '00000000-0000-0000-0000-000000000002', 'Gas', 'Sell');
                INSERT INTO physical_deliveries (contract_id, contract_instance_id, book_type, supply_month, revenue_eur) VALUES ('00000000-0000-0000-0000-000000000003', 'CP45.SC.2601.ETS-1-2026', 'Sales', '2026-01-01', 1234.56);
                """;
            var command = new NpgsqlCommand(sql, connection);
            await using (command.ConfigureAwait(false))
            {
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }
    }
}
