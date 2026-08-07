using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Npgsql;
using Tradebook.Api.Features.Analytics;
using Tradebook.Api.Features.Dashboards;
using Tradebook.Core.Analytics;
using Tradebook.Core.DTOs;
using Tradebook.Infrastructure.Data;

namespace Tradebook.UnitTests;

public sealed class AnalyticsAndDashboardEndpointMutationTests
{
    [Fact]
    public async Task Analytics_semantic_validation_failure_returns_400_without_opening_postgres()
    {
        var connections = new ThrowingConnectionFactory(new InvalidOperationException("Postgres must not be opened."));
        var endpoint = Factory.Create<AnalyticsQueryEndpoint>(
            new SemanticQueryCompiler(new SemanticModelLoader()),
            connections);
        var query = new JsonQueryAst(
            "delivery_pnl_analytics",
            Measures: null,
            Metrics: null,
            Dimensions: null,
            TimeDimensions: null,
            Filters: null,
            Sorts: null,
            Limit: null,
            Offset: null);

        await endpoint.HandleAsync(query, default);

        Assert.Equal(400, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal(0, connections.OpenCalls);
    }

    [Fact]
    public async Task Analytics_valid_query_reaches_postgres_with_the_exact_cancellation_token()
    {
        var marker = new InvalidOperationException("analytics-db-marker");
        var connections = new ThrowingConnectionFactory(marker);
        var endpoint = Factory.Create<AnalyticsQueryEndpoint>(
            new SemanticQueryCompiler(new SemanticModelLoader()),
            connections);
        using var cancellation = new CancellationTokenSource();
        var query = new JsonQueryAst(
            "delivery_pnl_analytics",
            Measures: ["volume_mwh"],
            Metrics: null,
            Dimensions: null,
            TimeDimensions: null,
            Filters: null,
            Sorts: null,
            Limit: 1,
            Offset: 0);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => endpoint.HandleAsync(query, cancellation.Token));

        Assert.Same(marker, thrown);
        Assert.Equal(1, connections.OpenCalls);
        Assert.Equal(cancellation.Token, connections.LastCancellationToken);
    }

    [Fact]
    public async Task Dashboard_invalid_layout_returns_400_before_actor_or_postgres_resolution()
    {
        var connections = new ThrowingConnectionFactory(new InvalidOperationException("Postgres must not be opened."));
        var endpoint = Factory.Create<SaveDashboardEndpoint>(
            connections,
            new SemanticQueryCompiler(new SemanticModelLoader()),
            DashboardJsonOptions());
        var request = new SaveDashboardRequest(
            Guid.NewGuid(),
            0,
            JsonSerializer.SerializeToElement(new { }));

        await endpoint.HandleAsync(request, default);

        Assert.Equal(400, endpoint.HttpContext.Response.StatusCode);
        var failure = Assert.Single(endpoint.ValidationFailures);
        Assert.Equal("GeneralErrors", failure.PropertyName);
        Assert.Equal("Missing required property 'dashboardId'.", failure.ErrorMessage);
        Assert.Equal(0, connections.OpenCalls);
    }

    [Fact]
    public async Task Dashboard_valid_layout_reaches_postgres_after_resolving_the_actor()
    {
        var actorId = Guid.NewGuid();
        var dashboardId = Guid.NewGuid();
        var marker = new InvalidOperationException("dashboard-save-db-marker");
        var connections = new ThrowingConnectionFactory(marker);
        using var cancellation = new CancellationTokenSource();
        var endpoint = Factory.Create<SaveDashboardEndpoint>(
            context => context.User = Principal(actorId),
            connections,
            new SemanticQueryCompiler(new SemanticModelLoader()),
            DashboardJsonOptions());

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => endpoint.HandleAsync(
            new SaveDashboardRequest(dashboardId, 0, DashboardLayout(dashboardId)),
            cancellation.Token));

        Assert.Same(marker, thrown);
        Assert.Equal(1, connections.OpenCalls);
        Assert.Equal(cancellation.Token, connections.LastCancellationToken);
    }

    [Fact]
    public async Task Dashboard_get_reaches_postgres_with_the_authenticated_actor_context_established()
    {
        var actorId = Guid.NewGuid();
        var marker = new InvalidOperationException("dashboard-get-db-marker");
        var connections = new ThrowingConnectionFactory(marker);
        using var cancellation = new CancellationTokenSource();
        var endpoint = Factory.Create<GetDashboardEndpoint>(
            context => context.User = Principal(actorId),
            connections);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => endpoint.HandleAsync(
            new GetDashboardRequest { DashboardId = Guid.NewGuid() },
            cancellation.Token));

        Assert.Same(marker, thrown);
        Assert.Equal(1, connections.OpenCalls);
        Assert.Equal(cancellation.Token, connections.LastCancellationToken);
    }

    private static IOptions<JsonOptions> DashboardJsonOptions()
    {
        var options = new JsonOptions();
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        return Microsoft.Extensions.Options.Options.Create(options);
    }

    private static ClaimsPrincipal Principal(Guid actorId) =>
        new(new ClaimsIdentity([new Claim("sub", actorId.ToString())], "test"));

    private static JsonElement DashboardLayout(Guid dashboardId) => JsonSerializer.SerializeToElement(new
    {
        dashboardId,
        title = "Dashboard",
        version = 0,
        theme = "SYSTEM",
        refreshRateMs = 30_000,
        gridLayout = new
        {
            columns = 12,
            rowHeight = 30,
            items = new[] { new { widgetId = "chart-1", x = 0, y = 0, w = 6, h = 4 } }
        },
        widgets = new[]
        {
            new
            {
                id = "chart-1",
                title = "Chart",
                chartType = "LINE",
                semanticModelRef = "delivery_pnl_analytics",
                queryAst = new
                {
                    modelName = "delivery_pnl_analytics",
                    dimensions = new[] { "supply_month" },
                    measures = new[] { "volume_mwh" }
                },
                visualEncodings = new { xAxis = "supply_month", yAxis = new[] { "volume_mwh" } }
            }
        }
    });

    private sealed class ThrowingConnectionFactory(Exception exception) : INpgsqlConnectionFactory
    {
        public int OpenCalls { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            OpenCalls++;
            LastCancellationToken = cancellationToken;
            return ValueTask.FromException<NpgsqlConnection>(exception);
        }
    }
}
