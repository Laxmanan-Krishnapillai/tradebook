using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Client;
using Npgsql;
using Tradebook.Api.AgentTools;
using Tradebook.Core.Analytics;
using Tradebook.IntegrationTests.Fixtures;

namespace Tradebook.IntegrationTests;

public sealed class McpAnalyticsIntegrationTests(CustomWebApplicationFactory factory)
    : DatabaseTestBase(factory)
{
    private static readonly string[] RevenueMeasures = ["revenue_eur"];
    private static readonly string[] BookTypeDimensions = ["book_type"];

    [Fact]
    [Trait("Category", "McpCapability")]
    public async Task McpRequiresAJwt()
    {
        using var response = await Client.PostAsync(
            "/mcp",
            new StringContent(InitializeRequest, Encoding.UTF8, "application/json")
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "McpCapability")]
    public async Task McpRequiresAReadRole()
    {
        Authenticate(includeReadRole: false);

        using var response = await Client.PostAsync(
            "/mcp",
            new StringContent(InitializeRequest, Encoding.UTF8, "application/json")
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "McpCapability")]
    public async Task AuthenticatedClientDiscoversAndExecutesTheSharedAnalyticsCapability()
    {
        await SeedAsync();
        Authenticate(includeReadRole: true);
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(Client.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
                EnableStandaloneGetStream = false,
            },
            Client,
            loggerFactory: null,
            ownsHttpClient: false
        );
        await using var mcp = await McpClient.CreateAsync(transport);

        var tools = await mcp.ListToolsAsync();
        var tool = Assert.Single(
            tools,
            candidate =>
                string.Equals(
                    candidate.Name,
                    AiCapabilityCatalog.AnalyticsQueryMcpTool,
                    StringComparison.Ordinal
                )
        );
        Assert.True(tool.ProtocolTool.Annotations?.ReadOnlyHint);
        Assert.True(tool.ProtocolTool.Annotations?.IdempotentHint);
        Assert.False(tool.ProtocolTool.Annotations?.OpenWorldHint);

        var result = await tool.CallAsync(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["query"] = new JsonQueryAst(
                    "delivery_pnl_analytics",
                    Measures: RevenueMeasures,
                    Metrics: null,
                    Dimensions: BookTypeDimensions,
                    TimeDimensions: null,
                    Filters: null,
                    Sorts: null,
                    Limit: null,
                    Offset: null
                ),
            }
        );

        Assert.NotEqual(true, result.IsError);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result.StructuredContent));
        Assert.Equal("book_type", document.RootElement.GetProperty("columns")[0].GetString());
        Assert.Equal("revenue_eur", document.RootElement.GetProperty("columns")[1].GetString());
        Assert.Equal("Sales", document.RootElement.GetProperty("rows")[0][0].GetString());
        var revenue = document.RootElement.GetProperty("rows")[0][1].GetString();
        Assert.Equal(
            1234.56m,
            decimal.Parse(revenue!, NumberStyles.Number, CultureInfo.InvariantCulture)
        );
    }

    private void Authenticate(bool includeReadRole) =>
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(includeReadRole)
        );

    private static string CreateToken(bool includeReadRole)
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new("sub", "opaque-pairwise-subject"),
            new("oid", Guid.NewGuid().ToString()),
        };
        if (includeReadRole)
        {
            claims.Add(new("role", "Trader"));
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(CustomWebApplicationFactory.JwtSigningKey)
        );
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new System.Security.Claims.ClaimsIdentity(claims),
            Issuer = "Tradebook",
            Audience = "Tradebook",
            Expires = TimeProvider.System.GetUtcNow().UtcDateTime.AddMinutes(5),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
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

    private const string InitializeRequest = """
        {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"tradebook-tests","version":"1.0"}}}
        """;
}
