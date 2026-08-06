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

public sealed class AnalyticsQueryEndpointTests(CustomWebApplicationFactory factory) : DatabaseTestBase(factory)
{
    [Fact]
    public async Task Query_requires_a_jwt()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/analytics/query", new { modelName = "delivery_pnl_analytics", measures = new[] { "revenue_eur" } });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_filter_member_returns_bad_request()
    {
        Authenticate();
        var response = await Client.PostAsJsonAsync("/api/v1/analytics/query", new { modelName = "delivery_pnl_analytics", measures = new[] { "revenue_eur" }, filters = new[] { new { member = "not_a_member", @operator = "equals", values = new[] { "x" } } } });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Unknown filter member", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Seeded_delivery_returns_columns_and_rows()
    {
        await SeedAsync(); Authenticate();
        var response = await Client.PostAsJsonAsync("/api/v1/analytics/query", new { modelName = "delivery_pnl_analytics", dimensions = new[] { "book_type" }, measures = new[] { "revenue_eur" } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("book_type", document.RootElement.GetProperty("columns")[0].GetString());
        Assert.Equal("revenue_eur", document.RootElement.GetProperty("columns")[1].GetString());
        Assert.Equal("Sales", document.RootElement.GetProperty("rows")[0][0].GetString());
        Assert.Equal(1234.56m, document.RootElement.GetProperty("rows")[0][1].GetDecimal());
    }

    private void Authenticate() => Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());
    private static string CreateToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("development-only-signing-key-must-be-replaced"));
        var descriptor = new SecurityTokenDescriptor { Subject = new System.Security.Claims.ClaimsIdentity([new("sub", Guid.NewGuid().ToString()), new("role", "Trader")]), Issuer = "Tradebook", Audience = "Tradebook", Expires = DateTime.UtcNow.AddMinutes(5), SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256) };
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityTokenHandler().CreateToken(descriptor));
    }
    private async Task SeedAsync()
    {
        await using var connection = new NpgsqlConnection(Factory.ConnectionString); await connection.OpenAsync();
        const string sql = """
            INSERT INTO companies (id, shorthand, name) VALUES ('00000000-0000-0000-0000-000000000001', 'BGEM', 'BioGem');
            INSERT INTO counterparties (id, name, shorthand) VALUES ('00000000-0000-0000-0000-000000000002', 'Counterparty', 'CP');
            INSERT INTO contracts (id, contract_name, counterparty_id, product_type, action) VALUES ('00000000-0000-0000-0000-000000000003', 'CP45.SC.2601.ETS', '00000000-0000-0000-0000-000000000002', 'Gas', 'Sell');
            INSERT INTO physical_deliveries (contract_id, contract_instance_id, book_type, supply_month, revenue_eur) VALUES ('00000000-0000-0000-0000-000000000003', 'CP45.SC.2601.ETS-1-2026', 'Sales', '2026-01-01', 1234.56);
            """;
        await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync();
    }
}
