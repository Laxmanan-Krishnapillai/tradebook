using FastEndpoints;
using Tradebook.Api.Features.Hedges;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

public sealed class HandlerGroupBHedgeEndpointTests
{
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly CancellationTokenSource TokenSource = new();
    private static CancellationToken Token => TokenSource.Token;

    [Fact]
    public async Task Create_returns_201_and_forwards_the_exact_request_actor_and_token()
    {
        var expected = HandlerGroupBTestData.Hedge(version: 1);
        var repository = new RecordingHedgeRepository { CreateResult = expected };
        var endpoint = Factory.Create<CreateHedgeEndpoint>(
            context => context.User = HandlerGroupBTestData.Principal(ActorId), repository);
        var request = new CreateHedgeRequest(Guid.NewGuid(), new DateOnly(2026, 2, 1), 125m, 31.75m);

        await endpoint.HandleAsync(request, Token);

        Assert.Equal(201, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(expected, endpoint.Response);
        var call = Assert.Single(repository.CreateCalls);
        Assert.Same(request, call.Request);
        Assert.Equal(ActorId, call.ActorId);
        Assert.Equal(Token, call.Token);
        Assert.Empty(repository.GetByIdCalls);
    }

    [Fact]
    public async Task GetById_returns_200_with_the_repository_result_and_exact_call()
    {
        var hedgeId = Guid.NewGuid();
        var expected = HandlerGroupBTestData.Hedge(hedgeId);
        var repository = new RecordingHedgeRepository { GetByIdResult = expected };
        var endpoint = Create<GetHedgeByIdEndpoint>(repository);

        await endpoint.HandleAsync(new GetHedgeByIdRequest(hedgeId), Token);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(expected, endpoint.Response);
        Assert.Equal((hedgeId, Token), Assert.Single(repository.GetByIdCalls));
    }

    [Fact]
    public async Task GetById_returns_404_when_the_repository_has_no_row()
    {
        var hedgeId = Guid.NewGuid();
        var repository = new RecordingHedgeRepository { GetByIdResult = null };
        var endpoint = Create<GetHedgeByIdEndpoint>(repository);

        await endpoint.HandleAsync(new GetHedgeByIdRequest(hedgeId), Token);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal((hedgeId, Token), Assert.Single(repository.GetByIdCalls));
    }

    [Fact]
    public async Task History_returns_200_and_forwards_the_exact_request_and_token()
    {
        var expected = new GetHedgeHistoryResponse([HandlerGroupBTestData.Hedge()], 7, 2, 25, true);
        var repository = new RecordingHedgeRepository { HistoryResult = expected };
        var endpoint = Create<GetHedgeHistoryEndpoint>(repository);
        var request = new GetHedgeHistoryRequest(
            Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 1), 2, 25);

        await endpoint.HandleAsync(request, Token);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(expected, endpoint.Response);
        var call = Assert.Single(repository.HistoryCalls);
        Assert.Same(request, call.Request);
        Assert.Equal(Token, call.Token);
    }

    [Fact]
    public async Task Update_returns_200_and_does_not_issue_a_conflict_lookup()
    {
        var hedgeId = Guid.NewGuid();
        var expected = HandlerGroupBTestData.Hedge(hedgeId, version: 4);
        var repository = new RecordingHedgeRepository { UpdateResult = expected };
        var endpoint = Create<UpdateHedgeEndpoint>(repository);
        var request = new UpdateHedgeRequest(hedgeId, 130m, null, 3);

        await endpoint.HandleAsync(request, Token);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(expected, endpoint.Response);
        AssertUpdateCall(repository, request);
        Assert.Empty(repository.GetByIdCalls);
    }

    [Fact]
    public async Task Update_returns_404_after_a_failed_update_and_missing_lookup()
    {
        var hedgeId = Guid.NewGuid();
        var repository = new RecordingHedgeRepository { UpdateResult = null, GetByIdResult = null };
        var endpoint = Create<UpdateHedgeEndpoint>(repository);
        var request = new UpdateHedgeRequest(hedgeId, null, 32m, 3);

        await endpoint.HandleAsync(request, Token);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        AssertUpdateCall(repository, request);
        Assert.Equal((hedgeId, Token), Assert.Single(repository.GetByIdCalls));
    }

    [Fact]
    public async Task Update_returns_409_with_current_state_after_a_failed_update()
    {
        var hedgeId = Guid.NewGuid();
        var current = HandlerGroupBTestData.Hedge(hedgeId, version: 8);
        var repository = new RecordingHedgeRepository { UpdateResult = null, GetByIdResult = current };
        var endpoint = Create<UpdateHedgeEndpoint>(repository);
        var request = new UpdateHedgeRequest(hedgeId, 130m, null, 3);

        await endpoint.HandleAsync(request, Token);

        Assert.Equal(409, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(current, endpoint.Response);
        AssertUpdateCall(repository, request);
        Assert.Equal((hedgeId, Token), Assert.Single(repository.GetByIdCalls));
    }

    [Fact]
    public async Task Delete_returns_204_and_forwards_every_repository_argument()
    {
        var hedgeId = Guid.NewGuid();
        var repository = new RecordingHedgeRepository { DeleteResult = null };
        var endpoint = Create<DeleteHedgeEndpoint>(repository);
        var request = new DeleteHedgeRequest(hedgeId, "duplicate hedge", 6);

        await endpoint.HandleAsync(request, Token);

        Assert.Equal(204, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal((hedgeId, 6L, "duplicate hedge", ActorId, Token), Assert.Single(repository.DeleteCalls));
        Assert.Empty(repository.GetByIdCalls);
    }

    [Fact]
    public async Task Delete_returns_404_without_loading_current_state_for_not_found()
    {
        var hedgeId = Guid.NewGuid();
        var repository = new RecordingHedgeRepository { DeleteResult = MutationOutcome.NotFound };
        var endpoint = Create<DeleteHedgeEndpoint>(repository);

        await endpoint.HandleAsync(new DeleteHedgeRequest(hedgeId, "duplicate hedge", 6), Token);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal((hedgeId, 6L, "duplicate hedge", ActorId, Token), Assert.Single(repository.DeleteCalls));
        Assert.Empty(repository.GetByIdCalls);
    }

    [Fact]
    public async Task Delete_returns_409_with_current_state_for_version_conflict()
    {
        var hedgeId = Guid.NewGuid();
        var current = HandlerGroupBTestData.Hedge(hedgeId, version: 9);
        var repository = new RecordingHedgeRepository
        {
            DeleteResult = MutationOutcome.VersionConflict,
            GetByIdResult = current,
        };
        var endpoint = Create<DeleteHedgeEndpoint>(repository);

        await endpoint.HandleAsync(new DeleteHedgeRequest(hedgeId, "duplicate hedge", 6), Token);

        Assert.Equal(409, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(current, endpoint.Response);
        Assert.Equal((hedgeId, 6L, "duplicate hedge", ActorId, Token), Assert.Single(repository.DeleteCalls));
        Assert.Equal((hedgeId, Token), Assert.Single(repository.GetByIdCalls));
    }

    private static TEndpoint Create<TEndpoint>(RecordingHedgeRepository repository)
        where TEndpoint : BaseEndpoint =>
        Factory.Create<TEndpoint>(
            context => context.User = HandlerGroupBTestData.Principal(ActorId), repository);

    private static void AssertUpdateCall(RecordingHedgeRepository repository, UpdateHedgeRequest request)
    {
        var call = Assert.Single(repository.UpdateCalls);
        Assert.Same(request, call.Request);
        Assert.Equal(ActorId, call.ActorId);
        Assert.Equal(Token, call.Token);
    }
}
