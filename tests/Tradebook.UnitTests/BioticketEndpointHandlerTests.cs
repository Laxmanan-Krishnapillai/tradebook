using FastEndpoints;
using Tradebook.Api.Features.Biotickets;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

public sealed class BioticketEndpointHandlerTests
{
    private static readonly Guid ActorId = Guid.Parse("f5337077-c98b-45a5-9689-0b64668bdbb1");

    private static CreateBioticketRequest CreateRequest() => new(
        Guid.NewGuid(),
        "Sales",
        new DateOnly(2026, 1, 1),
        "CRSB45.ST.2401.CO2E-1-2026",
        new DateOnly(2026, 1, 1),
        new DateOnly(2026, 1, 31),
        12m,
        11m,
        10m,
        20m,
        200m,
        0.25m,
        50m,
        250m,
        "Awaiting",
        "handler fixture");

    private static UpdateBioticketRequest UpdateRequest(Guid id) => new(
        id,
        11m,
        10m,
        20m,
        200m,
        0.25m,
        50m,
        250m,
        "Awaiting",
        "corrected",
        3);

    [Fact]
    public async Task Create_returns_201_and_forwards_exact_request_actor_and_token()
    {
        var repository = new FakeBioticketEndpointRepository
        {
            CreateResult = DomainEndpointTestData.Bioticket(version: 1)
        };
        using var cancellation = new CancellationTokenSource();
        var endpoint = Factory.Create<CreateBioticketEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId), repository);
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
    public async Task Get_by_id_returns_200_and_forwards_exact_id()
    {
        var id = Guid.NewGuid();
        var repository = new FakeBioticketEndpointRepository
        {
            GetByIdResult = DomainEndpointTestData.Bioticket(id)
        };
        using var cancellation = new CancellationTokenSource();
        var endpoint = Factory.Create<GetBioticketByIdEndpoint>(repository);

        await endpoint.HandleAsync(new GetBioticketByIdRequest(id), cancellation.Token);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(repository.GetByIdResult, endpoint.Response);
        var call = Assert.Single(repository.GetByIdCalls);
        Assert.Equal(id, call.Id);
        Assert.Equal(cancellation.Token, call.Token);
    }

    [Fact]
    public async Task Get_by_id_returns_404_when_repository_has_no_match()
    {
        var id = Guid.NewGuid();
        var repository = new FakeBioticketEndpointRepository { GetByIdResult = null };
        var endpoint = Factory.Create<GetBioticketByIdEndpoint>(repository);

        await endpoint.HandleAsync(new GetBioticketByIdRequest(id), default);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal(id, Assert.Single(repository.GetByIdCalls).Id);
    }

    [Fact]
    public async Task History_returns_200_and_forwards_the_exact_filter_object()
    {
        var repository = new FakeBioticketEndpointRepository
        {
            HistoryResult = new([DomainEndpointTestData.Bioticket()], 1, 3, 25, false)
        };
        var request = new GetBioticketHistoryRequest(
            Guid.NewGuid(), "Sales", "Awaiting", new DateOnly(2026, 1, 1),
            new DateOnly(2026, 3, 1), 3, 25);
        using var cancellation = new CancellationTokenSource();
        var endpoint = Factory.Create<GetBioticketHistoryEndpoint>(repository);

        await endpoint.HandleAsync(request, cancellation.Token);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(repository.HistoryResult, endpoint.Response);
        var call = Assert.Single(repository.HistoryCalls);
        Assert.Same(request, call.Request);
        Assert.Equal(cancellation.Token, call.Token);
    }

    [Fact]
    public async Task Update_success_returns_200_and_does_not_fetch_current_state()
    {
        var id = Guid.NewGuid();
        var repository = new FakeBioticketEndpointRepository
        {
            UpdateResult = DomainEndpointTestData.Bioticket(id, version: 4)
        };
        var request = UpdateRequest(id);
        using var cancellation = new CancellationTokenSource();
        var endpoint = Factory.Create<UpdateBioticketEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId), repository);

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
    public async Task Update_returns_404_when_update_and_current_lookup_are_missing()
    {
        var id = Guid.NewGuid();
        var repository = new FakeBioticketEndpointRepository
        {
            UpdateResult = null,
            GetByIdResult = null
        };
        var request = UpdateRequest(id);
        var endpoint = Factory.Create<UpdateBioticketEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId), repository);

        await endpoint.HandleAsync(request, default);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        var updateCall = Assert.Single(repository.UpdateCalls);
        Assert.Same(request, updateCall.Request);
        Assert.Equal(ActorId, updateCall.ActorId);
        Assert.Equal(id, Assert.Single(repository.GetByIdCalls).Id);
    }

    [Fact]
    public async Task Update_returns_409_with_current_state_on_version_conflict()
    {
        var id = Guid.NewGuid();
        var current = DomainEndpointTestData.Bioticket(id, version: 8);
        var repository = new FakeBioticketEndpointRepository
        {
            UpdateResult = null,
            GetByIdResult = current
        };
        var request = UpdateRequest(id);
        var endpoint = Factory.Create<UpdateBioticketEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId), repository);

        await endpoint.HandleAsync(request, default);

        Assert.Equal(409, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(current, endpoint.Response);
        var updateCall = Assert.Single(repository.UpdateCalls);
        Assert.Same(request, updateCall.Request);
        Assert.Equal(ActorId, updateCall.ActorId);
        Assert.Equal(id, Assert.Single(repository.GetByIdCalls).Id);
    }

    [Fact]
    public async Task Cancel_success_returns_204_and_forwards_all_mutation_arguments()
    {
        var id = Guid.NewGuid();
        var repository = new FakeBioticketEndpointRepository { CancelResult = null };
        var request = new CancelBioticketRequest(id, "Duplicate ticket", 3);
        using var cancellation = new CancellationTokenSource();
        var endpoint = Factory.Create<CancelBioticketEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId), repository);

        await endpoint.HandleAsync(request, cancellation.Token);

        Assert.Equal(204, endpoint.HttpContext.Response.StatusCode);
        var call = Assert.Single(repository.CancelCalls);
        Assert.Equal(id, call.Id);
        Assert.Equal(3, call.Version);
        Assert.Equal("Duplicate ticket", call.Reason);
        Assert.Equal(ActorId, call.ActorId);
        Assert.Equal(cancellation.Token, call.Token);
        Assert.Empty(repository.GetByIdCalls);
    }

    [Fact]
    public async Task Cancel_returns_404_for_not_found_without_fetching_current_state()
    {
        var id = Guid.NewGuid();
        var repository = new FakeBioticketEndpointRepository { CancelResult = MutationOutcome.NotFound };
        var request = new CancelBioticketRequest(id, "Duplicate ticket", 3);
        var endpoint = Factory.Create<CancelBioticketEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId), repository);

        await endpoint.HandleAsync(request, default);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        var call = Assert.Single(repository.CancelCalls);
        Assert.Equal((id, 3L, "Duplicate ticket", ActorId),
            (call.Id, call.Version, call.Reason, call.ActorId));
        Assert.Empty(repository.GetByIdCalls);
    }

    [Fact]
    public async Task Cancel_returns_409_with_current_state_for_version_conflict()
    {
        var id = Guid.NewGuid();
        var current = DomainEndpointTestData.Bioticket(id, version: 9);
        var repository = new FakeBioticketEndpointRepository
        {
            CancelResult = MutationOutcome.VersionConflict,
            GetByIdResult = current
        };
        var request = new CancelBioticketRequest(id, "Duplicate ticket", 3);
        var endpoint = Factory.Create<CancelBioticketEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId), repository);

        await endpoint.HandleAsync(request, default);

        Assert.Equal(409, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(current, endpoint.Response);
        var call = Assert.Single(repository.CancelCalls);
        Assert.Equal((id, 3L, "Duplicate ticket", ActorId),
            (call.Id, call.Version, call.Reason, call.ActorId));
        Assert.Equal(id, Assert.Single(repository.GetByIdCalls).Id);
    }
}
