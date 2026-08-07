using FastEndpoints;
using Tradebook.Api.Features.Contracts;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

public sealed class ContractEndpointHandlerTests
{
    private static readonly Guid ActorId = Guid.Parse("3f45f1ad-5e63-4a11-abec-b9175bc74212");

    private static CreateContractRequest CreateRequest() => new(
        "ARLA45.SC.2601.ETSS",
        Guid.NewGuid(),
        "GoO",
        "Sell",
        "ARLA",
        "DK",
        45,
        1,
        2026,
        Guid.NewGuid(),
        Guid.NewGuid(),
        "BGEM",
        "ETS",
        "SUB",
        "TTF",
        31.25m,
        "External",
        "Annual certificate supply");

    private static UpdateContractRequest UpdateRequest(Guid id) => new(
        id,
        "ARLA45.SC.2601.ETSS",
        Guid.NewGuid(),
        "GoO",
        "Sell",
        "ARLA",
        "DK",
        45,
        Guid.NewGuid(),
        Guid.NewGuid(),
        "BGEM",
        "ETS",
        "SUB",
        "TTF",
        31.25m,
        "External",
        "Adjusted annual certificate supply",
        true,
        3);

    [Fact]
    public async Task Create_returns_201_and_forwards_exact_request_actor_and_token()
    {
        var repository = new FakeContractEndpointRepository
        {
            CreateResult = DomainEndpointTestData.Contract(version: 1)
        };
        using var cancellation = new CancellationTokenSource();
        var endpoint = Factory.Create<CreateContractEndpoint>(
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
        var repository = new FakeContractEndpointRepository
        {
            GetByIdResult = DomainEndpointTestData.Contract(id)
        };
        using var cancellation = new CancellationTokenSource();
        var endpoint = Factory.Create<GetContractByIdEndpoint>(repository);

        await endpoint.HandleAsync(new GetContractByIdRequest(id), cancellation.Token);

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
        var repository = new FakeContractEndpointRepository { GetByIdResult = null };
        var endpoint = Factory.Create<GetContractByIdEndpoint>(repository);

        await endpoint.HandleAsync(new GetContractByIdRequest(id), default);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal(id, Assert.Single(repository.GetByIdCalls).Id);
    }

    [Fact]
    public async Task History_returns_200_and_forwards_the_exact_filter_object()
    {
        var repository = new FakeContractEndpointRepository
        {
            HistoryResult = new([DomainEndpointTestData.Contract()], 1, 2, 25, false)
        };
        var request = new GetContractHistoryRequest(Guid.NewGuid(), "GoO", "Sell", true, 2, 25);
        using var cancellation = new CancellationTokenSource();
        var endpoint = Factory.Create<GetContractHistoryEndpoint>(repository);

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
        var repository = new FakeContractEndpointRepository
        {
            UpdateResult = DomainEndpointTestData.Contract(id, version: 4)
        };
        var request = UpdateRequest(id);
        using var cancellation = new CancellationTokenSource();
        var endpoint = Factory.Create<UpdateContractEndpoint>(
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
        var repository = new FakeContractEndpointRepository
        {
            UpdateResult = null,
            GetByIdResult = null
        };
        var request = UpdateRequest(id);
        var endpoint = Factory.Create<UpdateContractEndpoint>(
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
        var current = DomainEndpointTestData.Contract(id, version: 8);
        var repository = new FakeContractEndpointRepository
        {
            UpdateResult = null,
            GetByIdResult = current
        };
        var request = UpdateRequest(id);
        var endpoint = Factory.Create<UpdateContractEndpoint>(
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
    public async Task Deactivate_success_returns_204_and_forwards_all_mutation_arguments()
    {
        var id = Guid.NewGuid();
        var repository = new FakeContractEndpointRepository { DeactivateResult = null };
        var request = new DeactivateContractRequest(id, "Superseded by renewed contract", 3);
        using var cancellation = new CancellationTokenSource();
        var endpoint = Factory.Create<DeactivateContractEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId), repository);

        await endpoint.HandleAsync(request, cancellation.Token);

        Assert.Equal(204, endpoint.HttpContext.Response.StatusCode);
        var call = Assert.Single(repository.DeactivateCalls);
        Assert.Equal(id, call.Id);
        Assert.Equal(3, call.Version);
        Assert.Equal("Superseded by renewed contract", call.Reason);
        Assert.Equal(ActorId, call.ActorId);
        Assert.Equal(cancellation.Token, call.Token);
        Assert.Empty(repository.GetByIdCalls);
    }

    [Fact]
    public async Task Deactivate_returns_404_for_not_found_without_fetching_current_state()
    {
        var id = Guid.NewGuid();
        var repository = new FakeContractEndpointRepository { DeactivateResult = MutationOutcome.NotFound };
        var request = new DeactivateContractRequest(id, "Superseded by renewed contract", 3);
        var endpoint = Factory.Create<DeactivateContractEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId), repository);

        await endpoint.HandleAsync(request, default);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        var call = Assert.Single(repository.DeactivateCalls);
        Assert.Equal((id, 3L, "Superseded by renewed contract", ActorId),
            (call.Id, call.Version, call.Reason, call.ActorId));
        Assert.Empty(repository.GetByIdCalls);
    }

    [Fact]
    public async Task Deactivate_returns_409_with_current_state_for_version_conflict()
    {
        var id = Guid.NewGuid();
        var current = DomainEndpointTestData.Contract(id, version: 9);
        var repository = new FakeContractEndpointRepository
        {
            DeactivateResult = MutationOutcome.VersionConflict,
            GetByIdResult = current
        };
        var request = new DeactivateContractRequest(id, "Superseded by renewed contract", 3);
        var endpoint = Factory.Create<DeactivateContractEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId), repository);

        await endpoint.HandleAsync(request, default);

        Assert.Equal(409, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(current, endpoint.Response);
        var call = Assert.Single(repository.DeactivateCalls);
        Assert.Equal((id, 3L, "Superseded by renewed contract", ActorId),
            (call.Id, call.Version, call.Reason, call.ActorId));
        Assert.Equal(id, Assert.Single(repository.GetByIdCalls).Id);
    }
}
