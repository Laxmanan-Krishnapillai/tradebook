using FastEndpoints;
using Npgsql;
using Tradebook.Api.Features.Activity;
using Tradebook.Infrastructure.Data;

namespace Tradebook.UnitTests;

public sealed class ActivityEndpointMutationTests
{
    public static TheoryData<string> AllowedEntityNames =>
        new()
        {
            "bioticket_deliveries",
            "capacity_bookings",
            "contracts",
            "goo_certificate_transactions",
            "hedges",
            "market_prices",
            "physical_deliveries",
            "tax_tariffs",
            "transfers",
        };

    public static TheoryData<string, string, int> InvalidRequests =>
        new()
        {
            { "unknown", "entity-1", 1 },
            { "contracts", "", 1 },
            { "contracts", " ", 1 },
            { "contracts", new string('x', 129), 1 },
            { "contracts", "entity-1", 0 },
            { "contracts", "entity-1", 201 },
        };

    [Theory]
    [MemberData(nameof(AllowedEntityNames))]
    public async Task EveryAllowedEntityReachesPostgres(string entityName)
    {
        var marker = new InvalidOperationException("activity-db-marker");
        var connections = new ThrowingConnectionFactory(marker);
        var endpoint = Factory.Create<GetActivityEndpoint>(connections);
        using var cancellation = new CancellationTokenSource();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            endpoint.HandleAsync(
                new GetActivityRequest
                {
                    EntityName = entityName,
                    EntityId = "entity-1",
                    PageSize = 1,
                },
                cancellation.Token
            )
        );

        Assert.Same(marker, thrown);
        Assert.Equal(1, connections.OpenCalls);
        Assert.Equal(cancellation.Token, connections.LastCancellationToken);
    }

    [Fact]
    public async Task InclusiveMaximumPageSizeReachesPostgres()
    {
        var marker = new InvalidOperationException("activity-db-marker");
        var connections = new ThrowingConnectionFactory(marker);
        var endpoint = Factory.Create<GetActivityEndpoint>(connections);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            endpoint.HandleAsync(
                new GetActivityRequest
                {
                    EntityName = "contracts",
                    EntityId = "entity-1",
                    PageSize = 200,
                },
                default
            )
        );

        Assert.Same(marker, thrown);
        Assert.Equal(1, connections.OpenCalls);
    }

    [Fact]
    public async Task InclusiveMaximumEntityIdLengthReachesPostgres()
    {
        var marker = new InvalidOperationException("activity-db-marker");
        var connections = new ThrowingConnectionFactory(marker);
        var endpoint = Factory.Create<GetActivityEndpoint>(connections);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            endpoint.HandleAsync(
                new GetActivityRequest
                {
                    EntityName = "contracts",
                    EntityId = new string('x', 128),
                    PageSize = 1,
                },
                default
            )
        );

        Assert.Same(marker, thrown);
        Assert.Equal(1, connections.OpenCalls);
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task InvalidRequestsReturn400WithoutOpeningPostgres(
        string entityName,
        string entityId,
        int pageSize
    )
    {
        var connections = new ThrowingConnectionFactory(
            new InvalidOperationException("Postgres must not be opened.")
        );
        var endpoint = Factory.Create<GetActivityEndpoint>(connections);

        await endpoint.HandleAsync(
            new GetActivityRequest
            {
                EntityName = entityName,
                EntityId = entityId,
                PageSize = pageSize,
            },
            default
        );

        Assert.Equal(400, endpoint.HttpContext.Response.StatusCode);
        var failure = Assert.Single(endpoint.ValidationFailures);
        Assert.Equal("GeneralErrors", failure.PropertyName);
        Assert.Equal("The requested activity stream is invalid.", failure.ErrorMessage);
        Assert.Equal(0, connections.OpenCalls);
    }

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
