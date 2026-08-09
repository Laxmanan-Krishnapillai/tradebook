using FastEndpoints;
using Tradebook.Api.Features.CapacityBookings;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

public sealed class CapacityBookingEndpointHandlerTests
{
    private static readonly Guid ActorId = Guid.Parse("dc6f1f4c-27ae-48e5-a268-c498a052aa0f");

    private static CreateCapacityBookingRequest CreateRequest() =>
        new(
            Guid.NewGuid(),
            new DateOnly(2026, 2, 1),
            "NRGD.49.GAS.THE.CBC.MON-2-2026",
            Guid.NewGuid(),
            "NRGD",
            "GTF/THE - Monthly",
            "GTF",
            "THE",
            "GTF-ELLUND-THE",
            "ELLUND",
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 2, 28),
            15m,
            2.5m,
            37.5m,
            "Reserve monthly capacity"
        );

    private static UpdateCapacityBookingRequest UpdateRequest(Guid id) =>
        new(
            id,
            "NRGD",
            "GTF/THE - Monthly",
            "GTF",
            "THE",
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 2, 28),
            15m,
            2.5m,
            37.5m,
            "Adjusted capacity",
            3
        );

    [Fact]
    public async Task CreateReturns201AndForwardsExactRequestActorAndToken()
    {
        var repository = new FakeCapacityBookingEndpointRepository
        {
            CreateResult = DomainEndpointTestData.CapacityBooking(version: 1),
        };
        using var cancellation = new CancellationTokenSource();
        var endpoint = Factory.Create<CreateCapacityBookingEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId),
            repository
        );
        var request = CreateRequest();

        await endpoint.HandleAsync(request, cancellation.Token);

        Assert.Equal(201, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(repository.CreateResult, endpoint.Response);
        var call = Assert.Single(repository.CreateCalls);
        Assert.Same(request, call.Request);
        Assert.Equal(ActorId, call.ActorId);
        Assert.Equal(cancellation.Token, call.Token);
    }

    [Fact]
    public async Task GetByIdReturns200AndForwardsExactId()
    {
        var id = Guid.NewGuid();
        var repository = new FakeCapacityBookingEndpointRepository
        {
            GetByIdResult = DomainEndpointTestData.CapacityBooking(id),
        };
        using var cancellation = new CancellationTokenSource();
        var endpoint = Factory.Create<GetCapacityBookingByIdEndpoint>(repository);

        await endpoint.HandleAsync(new GetCapacityBookingByIdRequest(id), cancellation.Token);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(repository.GetByIdResult, endpoint.Response);
        var call = Assert.Single(repository.GetByIdCalls);
        Assert.Equal(id, call.Id);
        Assert.Equal(cancellation.Token, call.Token);
    }

    [Fact]
    public async Task GetByIdReturns404WhenRepositoryHasNoMatch()
    {
        var id = Guid.NewGuid();
        var repository = new FakeCapacityBookingEndpointRepository { GetByIdResult = null };
        var endpoint = Factory.Create<GetCapacityBookingByIdEndpoint>(repository);

        await endpoint.HandleAsync(new GetCapacityBookingByIdRequest(id), default);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal(id, Assert.Single(repository.GetByIdCalls).Id);
    }

    [Fact]
    public async Task HistoryReturns200AndForwardsTheExactFilterObject()
    {
        var repository = new FakeCapacityBookingEndpointRepository
        {
            HistoryResult = new([DomainEndpointTestData.CapacityBooking()], 1, 2, 25, false),
        };
        var request = new GetCapacityBookingHistoryRequest(
            Guid.NewGuid(),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 3, 1),
            2,
            25
        );
        using var cancellation = new CancellationTokenSource();
        var endpoint = Factory.Create<GetCapacityBookingHistoryEndpoint>(repository);

        await endpoint.HandleAsync(request, cancellation.Token);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(repository.HistoryResult, endpoint.Response);
        var call = Assert.Single(repository.HistoryCalls);
        Assert.Same(request, call.Request);
        Assert.Equal(cancellation.Token, call.Token);
    }

    [Fact]
    public async Task UpdateSuccessReturns200AndDoesNotFetchCurrentState()
    {
        var id = Guid.NewGuid();
        var repository = new FakeCapacityBookingEndpointRepository
        {
            UpdateResult = DomainEndpointTestData.CapacityBooking(id, version: 4),
        };
        var request = UpdateRequest(id);
        using var cancellation = new CancellationTokenSource();
        var endpoint = Factory.Create<UpdateCapacityBookingEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId),
            repository
        );

        await endpoint.HandleAsync(request, cancellation.Token);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(repository.UpdateResult, endpoint.Response);
        var call = Assert.Single(repository.UpdateCalls);
        Assert.Same(request, call.Request);
        Assert.Equal(ActorId, call.ActorId);
        Assert.Equal(cancellation.Token, call.Token);
        Assert.Empty(repository.GetByIdCalls);
    }

    [Fact]
    public async Task UpdateReturns404WhenUpdateAndCurrentLookupAreMissing()
    {
        var id = Guid.NewGuid();
        var repository = new FakeCapacityBookingEndpointRepository
        {
            UpdateResult = null,
            GetByIdResult = null,
        };
        var request = UpdateRequest(id);
        var endpoint = Factory.Create<UpdateCapacityBookingEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId),
            repository
        );

        await endpoint.HandleAsync(request, default);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        var updateCall = Assert.Single(repository.UpdateCalls);
        Assert.Same(request, updateCall.Request);
        Assert.Equal(ActorId, updateCall.ActorId);
        Assert.Equal(id, Assert.Single(repository.GetByIdCalls).Id);
    }

    [Fact]
    public async Task UpdateReturns409WithCurrentStateOnVersionConflict()
    {
        var id = Guid.NewGuid();
        var current = DomainEndpointTestData.CapacityBooking(id, version: 8);
        var repository = new FakeCapacityBookingEndpointRepository
        {
            UpdateResult = null,
            GetByIdResult = current,
        };
        var request = UpdateRequest(id);
        var endpoint = Factory.Create<UpdateCapacityBookingEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId),
            repository
        );

        await endpoint.HandleAsync(request, default);

        Assert.Equal(409, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(current, endpoint.Response);
        var updateCall = Assert.Single(repository.UpdateCalls);
        Assert.Same(request, updateCall.Request);
        Assert.Equal(ActorId, updateCall.ActorId);
        Assert.Equal(id, Assert.Single(repository.GetByIdCalls).Id);
    }

    [Fact]
    public async Task DeleteSuccessReturns204AndForwardsAllMutationArguments()
    {
        var id = Guid.NewGuid();
        var repository = new FakeCapacityBookingEndpointRepository { DeleteResult = null };
        var request = new DeleteCapacityBookingRequest(id, "Booked against wrong route", 3);
        using var cancellation = new CancellationTokenSource();
        var endpoint = Factory.Create<DeleteCapacityBookingEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId),
            repository
        );

        await endpoint.HandleAsync(request, cancellation.Token);

        Assert.Equal(204, endpoint.HttpContext.Response.StatusCode);
        var call = Assert.Single(repository.DeleteCalls);
        Assert.Equal(id, call.Id);
        Assert.Equal(3, call.Version);
        Assert.Equal("Booked against wrong route", call.Reason);
        Assert.Equal(ActorId, call.ActorId);
        Assert.Equal(cancellation.Token, call.Token);
        Assert.Empty(repository.GetByIdCalls);
    }

    [Fact]
    public async Task DeleteReturns404ForNotFoundWithoutFetchingCurrentState()
    {
        var id = Guid.NewGuid();
        var repository = new FakeCapacityBookingEndpointRepository
        {
            DeleteResult = MutationOutcome.NotFound,
        };
        var request = new DeleteCapacityBookingRequest(id, "Booked against wrong route", 3);
        var endpoint = Factory.Create<DeleteCapacityBookingEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId),
            repository
        );

        await endpoint.HandleAsync(request, default);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        var call = Assert.Single(repository.DeleteCalls);
        Assert.Equal(
            (id, 3L, "Booked against wrong route", ActorId),
            (call.Id, call.Version, call.Reason, call.ActorId)
        );
        Assert.Empty(repository.GetByIdCalls);
    }

    [Fact]
    public async Task DeleteReturns409WithCurrentStateForVersionConflict()
    {
        var id = Guid.NewGuid();
        var current = DomainEndpointTestData.CapacityBooking(id, version: 9);
        var repository = new FakeCapacityBookingEndpointRepository
        {
            DeleteResult = MutationOutcome.VersionConflict,
            GetByIdResult = current,
        };
        var request = new DeleteCapacityBookingRequest(id, "Booked against wrong route", 3);
        var endpoint = Factory.Create<DeleteCapacityBookingEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId),
            repository
        );

        await endpoint.HandleAsync(request, default);

        Assert.Equal(409, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(current, endpoint.Response);
        var call = Assert.Single(repository.DeleteCalls);
        Assert.Equal(
            (id, 3L, "Booked against wrong route", ActorId),
            (call.Id, call.Version, call.Reason, call.ActorId)
        );
        Assert.Equal(id, Assert.Single(repository.GetByIdCalls).Id);
    }
}
