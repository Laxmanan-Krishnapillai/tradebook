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
    private static readonly string[] DashboardDimensions = ["supply_month"];
    private static readonly string[] DashboardMeasures = ["volume_mwh"];
    private static readonly string[] DashboardYAxis = ["volume_mwh"];

    [Fact]
    public async Task AnalyticsSemanticValidationFailureReturns400WithoutOpeningPostgres()
    {
        var connections = new ThrowingConnectionFactory(
            new InvalidOperationException("Postgres must not be opened.")
        );
        var endpoint = Factory.Create<AnalyticsQueryEndpoint>(
            new SemanticQueryCompiler(new SemanticModelLoader()),
            connections
        );
        var query = new JsonQueryAst(
            "delivery_pnl_analytics",
            Measures: null,
            Metrics: null,
            Dimensions: null,
            TimeDimensions: null,
            Filters: null,
            Sorts: null,
            Limit: null,
            Offset: null
        );

        await endpoint.HandleAsync(query, default);

        Assert.Equal(400, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal(0, connections.OpenCalls);
    }

    [Fact]
    public async Task AnalyticsValidQueryReachesPostgresWithTheExactCancellationToken()
    {
        var marker = new InvalidOperationException("analytics-db-marker");
        var connections = new ThrowingConnectionFactory(marker);
        var endpoint = Factory.Create<AnalyticsQueryEndpoint>(
            new SemanticQueryCompiler(new SemanticModelLoader()),
            connections
        );
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
            Offset: 0
        );

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            endpoint.HandleAsync(query, cancellation.Token)
        );

        Assert.Same(marker, thrown);
        Assert.Equal(1, connections.OpenCalls);
        Assert.Equal(cancellation.Token, connections.LastCancellationToken);
    }

    [Fact]
    public async Task DashboardInvalidLayoutReturns400BeforeActorOrPostgresResolution()
    {
        var connections = new ThrowingConnectionFactory(
            new InvalidOperationException("Postgres must not be opened.")
        );
        var endpoint = Factory.Create<SaveDashboardEndpoint>(
            connections,
            default(object)!,
            new SemanticQueryCompiler(new SemanticModelLoader()),
            DashboardJsonOptions()
        );
        var request = new SaveDashboardRequest(
            Guid.NewGuid(),
            0,
            JsonSerializer.SerializeToElement(new { })
        );

        await endpoint.HandleAsync(request, default);

        Assert.Equal(400, endpoint.HttpContext.Response.StatusCode);
        var failure = Assert.Single(endpoint.ValidationFailures);
        Assert.Equal("GeneralErrors", failure.PropertyName);
        Assert.Equal("Missing required property 'dashboardId'.", failure.ErrorMessage);
        Assert.Equal(0, connections.OpenCalls);
    }

    [Fact]
    public async Task DashboardValidLayoutReachesPostgresAfterResolvingTheActor()
    {
        var actorId = Guid.NewGuid();
        var dashboardId = Guid.NewGuid();
        var marker = new InvalidOperationException("dashboard-save-db-marker");
        var connections = new ThrowingConnectionFactory(marker);
        using var cancellation = new CancellationTokenSource();
        var endpoint = Factory.Create<SaveDashboardEndpoint>(
            context => context.User = Principal(actorId),
            connections,
            default(object)!,
            new SemanticQueryCompiler(new SemanticModelLoader()),
            DashboardJsonOptions()
        );

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            endpoint.HandleAsync(
                new SaveDashboardRequest(dashboardId, 0, DashboardLayout(dashboardId)),
                cancellation.Token
            )
        );

        Assert.Same(marker, thrown);
        Assert.Equal(1, connections.OpenCalls);
        Assert.Equal(cancellation.Token, connections.LastCancellationToken);
    }

    [Fact]
    public async Task DashboardGetReachesPostgresWithTheAuthenticatedActorContextEstablished()
    {
        var actorId = Guid.NewGuid();
        var marker = new InvalidOperationException("dashboard-get-db-marker");
        var connections = new ThrowingConnectionFactory(marker);
        using var cancellation = new CancellationTokenSource();
        var endpoint = Factory.Create<GetDashboardEndpoint>(
            context => context.User = Principal(actorId),
            connections
        );

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            endpoint.HandleAsync(
                new GetDashboardRequest { DashboardId = Guid.NewGuid() },
                cancellation.Token
            )
        );

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
        new(
            new ClaimsIdentity(
                [
                    new Claim("oid", actorId.ToString()),
                    new("tid", "11111111-1111-1111-1111-111111111111"),
                    new("tradebook_tenant", "11111111-1111-1111-1111-111111111111"),
                ],
                "test"
            )
        );

    private static JsonElement DashboardLayout(Guid dashboardId) =>
        JsonSerializer.SerializeToElement(
            new
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
                    items = new[]
                    {
                        new
                        {
                            widgetId = "chart-1",
                            x = 0,
                            y = 0,
                            w = 6,
                            h = 4,
                        },
                    },
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
                            dimensions = DashboardDimensions,
                            measures = DashboardMeasures,
                        },
                        visualEncodings = new { xAxis = "supply_month", yAxis = DashboardYAxis },
                    },
                },
            }
        );

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
