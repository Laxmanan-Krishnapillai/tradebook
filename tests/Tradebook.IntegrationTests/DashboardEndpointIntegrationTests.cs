using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Tradebook.IntegrationTests.Fixtures;

namespace Tradebook.IntegrationTests;

public sealed class DashboardEndpointIntegrationTests(PostgresTestFixture postgres) : PostgresDatabaseTestBase(postgres)
{
    [Fact]
    public async Task Dashboard_endpoints_require_a_jwt()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/dashboards/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_chart_type_is_rejected()
    {
        var dashboardId = Guid.NewGuid();
        await using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory, Guid.NewGuid());
        var response = await client.PutAsJsonAsync($"/api/v1/dashboards/{dashboardId}", new { dashboardId, version = 0, layout = Layout(dashboardId, 0, "NOT_A_CHART") });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_semantic_query_is_rejected_before_dashboard_or_event_persistence()
    {
        var dashboardId = Guid.NewGuid();
        await using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory, Guid.NewGuid());
        var layout = JsonSerializer.SerializeToNode(Layout(dashboardId, 0))!.AsObject();
        layout["widgets"]![0]!["queryAst"]!["measures"] =
            JsonSerializer.SerializeToNode(new[] { "not_a_measure" });

        var response = await client.PutAsJsonAsync(
            $"/api/v1/dashboards/{dashboardId}",
            new { dashboardId, version = 0, layout });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM workspace_dashboards WHERE id = @DashboardId",
            new { DashboardId = dashboardId }));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM realtime_event_log WHERE aggregate_id = @DashboardId",
            new { DashboardId = dashboardId.ToString() }));
    }

    [Fact]
    public async Task Layout_contract_requires_theme_and_positive_grid_sizes()
    {
        var dashboardId = Guid.NewGuid();
        await using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory, Guid.NewGuid());
        var missingTheme = JsonSerializer.SerializeToNode(Layout(dashboardId, 0))!.AsObject();
        missingTheme.Remove("theme");
        var zeroWidth = JsonSerializer.SerializeToNode(Layout(dashboardId, 0))!.AsObject();
        zeroWidth["gridLayout"]!["items"]![0]!["w"] = 0;

        var missingThemeResponse = await client.PutAsJsonAsync(
            $"/api/v1/dashboards/{dashboardId}",
            new { dashboardId, version = 0, layout = missingTheme });
        var zeroWidthResponse = await client.PutAsJsonAsync(
            $"/api/v1/dashboards/{dashboardId}",
            new { dashboardId, version = 0, layout = zeroWidth });

        Assert.Equal(HttpStatusCode.BadRequest, missingThemeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, zeroWidthResponse.StatusCode);
    }

    [Theory]
    [InlineData("yAxis")]
    [InlineData("tooltipFields")]
    [InlineData("measures")]
    public async Task Dashboard_string_array_bindings_must_be_non_empty_when_present(string binding)
    {
        var dashboardId = Guid.NewGuid();
        await using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory, Guid.NewGuid());
        var layout = JsonSerializer.SerializeToNode(Layout(dashboardId, 0))!.AsObject();
        var widget = layout["widgets"]![0]!;
        if (binding == "measures")
            widget["queryAst"]![binding] = new JsonArray();
        else
            widget["visualEncodings"]![binding] = new JsonArray();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/dashboards/{dashboardId}",
            new { dashboardId, version = 0, layout });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task Candlestick_widgets_require_exactly_four_ohlc_bindings(int bindingCount)
    {
        var dashboardId = Guid.NewGuid();
        await using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory, Guid.NewGuid());
        var layout = JsonSerializer.SerializeToNode(Layout(dashboardId, 0, "CANDLESTICK"))!.AsObject();
        layout["widgets"]![0]!["visualEncodings"]!["yAxis"] =
            JsonSerializer.SerializeToNode(Enumerable.Repeat("volume_mwh", bindingCount).ToArray());

        var response = await client.PutAsJsonAsync(
            $"/api/v1/dashboards/{dashboardId}",
            new { dashboardId, version = 0, layout });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Candlestick_widget_with_four_ohlc_bindings_is_accepted()
    {
        var dashboardId = Guid.NewGuid();
        await using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory, Guid.NewGuid());
        var layout = JsonSerializer.SerializeToNode(Layout(dashboardId, 0, "CANDLESTICK"))!.AsObject();
        layout["widgets"]![0]!["visualEncodings"]!["yAxis"] =
            JsonSerializer.SerializeToNode(Enumerable.Repeat("volume_mwh", 4).ToArray());

        var response = await client.PutAsJsonAsync(
            $"/api/v1/dashboards/{dashboardId}",
            new { dashboardId, version = 0, layout });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Actors_can_only_read_their_own_dashboards()
    {
        var dashboardId = Guid.NewGuid();
        await using var factory = CreateFactory();
        using var owner = AuthenticatedClient(factory, Guid.NewGuid());
        var saved = await owner.PutAsJsonAsync($"/api/v1/dashboards/{dashboardId}", new { dashboardId, version = 0, layout = Layout(dashboardId, 0) });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var other = AuthenticatedClient(factory, Guid.NewGuid());
        var response = await other.GetAsync($"/api/v1/dashboards/{dashboardId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Saves_and_reads_a_dashboard_for_its_actor()
    {
        var dashboardId = Guid.NewGuid();
        await using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory, Guid.NewGuid());
        var save = await client.PutAsJsonAsync($"/api/v1/dashboards/{dashboardId}", new { dashboardId, version = 0, layout = Layout(dashboardId, 0, title: "Operations") });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var read = await client.GetAsync($"/api/v1/dashboards/{dashboardId}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var body = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        Assert.Equal(1, body.RootElement.GetProperty("version").GetInt64());
        Assert.Equal("Operations", body.RootElement.GetProperty("layout").GetProperty("title").GetString());
        Assert.Equal(1, body.RootElement.GetProperty("layout").GetProperty("version").GetInt64());
    }

    [Fact]
    public async Task Stale_version_returns_current_server_dashboard_state()
    {
        var dashboardId = Guid.NewGuid();
        await using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/v1/dashboards/{dashboardId}", new { dashboardId, version = 0, layout = Layout(dashboardId, 0, title: "Initial") })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/v1/dashboards/{dashboardId}", new { dashboardId, version = 1, layout = Layout(dashboardId, 1, title: "Current") })).StatusCode);
        var stale = await client.PutAsJsonAsync($"/api/v1/dashboards/{dashboardId}", new { dashboardId, version = 1, layout = Layout(dashboardId, 1, title: "Stale") });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var body = JsonDocument.Parse(await stale.Content.ReadAsStringAsync());
        Assert.Equal(2, body.RootElement.GetProperty("version").GetInt64());
        Assert.Equal("Current", body.RootElement.GetProperty("layout").GetProperty("title").GetString());
    }

    private WebApplicationFactory<Program> CreateFactory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseSetting("Database:ConnectionString", Postgres.ConnectionString);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Database:ConnectionString"] = Postgres.ConnectionString, ["Jwt:Issuer"] = "Tradebook", ["Jwt:Audience"] = "Tradebook", ["Jwt:SigningKey"] = CustomWebApplicationFactory.JwtSigningKey }));
    });
    private static HttpClient AuthenticatedClient(WebApplicationFactory<Program> factory, Guid actorId) { var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(actorId)); return client; }
    private static string Token(Guid actorId)
    {
        var descriptor = new SecurityTokenDescriptor { Issuer = "Tradebook", Audience = "Tradebook", Subject = new ClaimsIdentity([new Claim("sub", actorId.ToString()), new Claim("role", "Trader")]), Expires = DateTime.UtcNow.AddMinutes(5), SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(CustomWebApplicationFactory.JwtSigningKey)), SecurityAlgorithms.HmacSha256) };
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityTokenHandler().CreateToken(descriptor));
    }
    private static object Layout(Guid id, long version, string chartType = "LINE", string title = "Dashboard") => new
    {
        dashboardId = id, title, version, theme = "SYSTEM", refreshRateMs = 30000,
        gridLayout = new { columns = 12, rowHeight = 30, items = new[] { new { widgetId = "chart-1", x = 0, y = 0, w = 6, h = 4 } } },
        widgets = new[] { new { id = "chart-1", title = "Chart", chartType, semanticModelRef = "delivery_pnl_analytics", queryAst = new { modelName = "delivery_pnl_analytics", dimensions = new[] { "supply_month" }, measures = new[] { "volume_mwh" } }, visualEncodings = new { xAxis = "supply_month", yAxis = new[] { "volume_mwh" } } } }
    };
}
