using FastEndpoints;
using Tradebook.Api.Features.Transfers;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

public sealed class HandlerGroupBTransferEndpointTests
{
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly CancellationTokenSource TokenSource = new();
    private static CancellationToken Token => TokenSource.Token;

    [Fact]
    public async Task Create_returns_201_and_forwards_the_exact_request_actor_and_token()
    {
        var expected = HandlerGroupBTestData.Transfer(version: 1);
        var repository = new RecordingTransferRepository { CreateResult = expected };
        var endpoint = Create<CreateTransferEndpoint>(repository);
        var request = CreateRequest();

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
        var transferId = Guid.NewGuid();
        var expected = HandlerGroupBTestData.Transfer(transferId);
        var repository = new RecordingTransferRepository { GetByIdResult = expected };
        var endpoint = Create<GetTransferByIdEndpoint>(repository);

        await endpoint.HandleAsync(new GetTransferByIdRequest(transferId), Token);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(expected, endpoint.Response);
        Assert.Equal((transferId, Token), Assert.Single(repository.GetByIdCalls));
    }

    [Fact]
    public async Task GetById_returns_404_when_the_repository_has_no_row()
    {
        var transferId = Guid.NewGuid();
        var repository = new RecordingTransferRepository { GetByIdResult = null };
        var endpoint = Create<GetTransferByIdEndpoint>(repository);

        await endpoint.HandleAsync(new GetTransferByIdRequest(transferId), Token);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal((transferId, Token), Assert.Single(repository.GetByIdCalls));
    }

    [Fact]
    public async Task History_returns_200_and_forwards_the_exact_request_and_token()
    {
        var expected = new GetTransferHistoryResponse(
            [HandlerGroupBTestData.Transfer()], 14, 2, 25, true);
        var repository = new RecordingTransferRepository { HistoryResult = expected };
        var endpoint = Create<GetTransferHistoryEndpoint>(repository);
        var request = new GetTransferHistoryRequest(
            Guid.NewGuid(), "Awaiting", new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 1), 2, 25);

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
        var transferId = Guid.NewGuid();
        var expected = HandlerGroupBTestData.Transfer(transferId, version: 4);
        var repository = new RecordingTransferRepository { UpdateResult = expected };
        var endpoint = Create<UpdateTransferEndpoint>(repository);
        var request = UpdateRequest(transferId, version: 3);

        await endpoint.HandleAsync(request, Token);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(expected, endpoint.Response);
        AssertUpdateCall(repository, request);
        Assert.Empty(repository.GetByIdCalls);
    }

    [Fact]
    public async Task Update_returns_404_after_a_failed_update_and_missing_lookup()
    {
        var transferId = Guid.NewGuid();
        var repository = new RecordingTransferRepository { UpdateResult = null, GetByIdResult = null };
        var endpoint = Create<UpdateTransferEndpoint>(repository);
        var request = UpdateRequest(transferId, version: 3);

        await endpoint.HandleAsync(request, Token);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        AssertUpdateCall(repository, request);
        Assert.Equal((transferId, Token), Assert.Single(repository.GetByIdCalls));
    }

    [Fact]
    public async Task Update_returns_409_with_current_state_after_a_failed_update()
    {
        var transferId = Guid.NewGuid();
        var current = HandlerGroupBTestData.Transfer(transferId, version: 8);
        var repository = new RecordingTransferRepository { UpdateResult = null, GetByIdResult = current };
        var endpoint = Create<UpdateTransferEndpoint>(repository);
        var request = UpdateRequest(transferId, version: 3);

        await endpoint.HandleAsync(request, Token);

        Assert.Equal(409, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(current, endpoint.Response);
        AssertUpdateCall(repository, request);
        Assert.Equal((transferId, Token), Assert.Single(repository.GetByIdCalls));
    }

    [Fact]
    public async Task Cancel_returns_204_and_forwards_every_repository_argument()
    {
        var transferId = Guid.NewGuid();
        var repository = new RecordingTransferRepository { CancelResult = null };
        var endpoint = Create<CancelTransferEndpoint>(repository);

        await endpoint.HandleAsync(new CancelTransferRequest(transferId, "capacity no longer needed", 6), Token);

        Assert.Equal(204, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal(
            (transferId, 6L, "capacity no longer needed", ActorId, Token),
            Assert.Single(repository.CancelCalls));
        Assert.Empty(repository.GetByIdCalls);
    }

    [Fact]
    public async Task Cancel_returns_404_without_loading_current_state_for_not_found()
    {
        var transferId = Guid.NewGuid();
        var repository = new RecordingTransferRepository { CancelResult = MutationOutcome.NotFound };
        var endpoint = Create<CancelTransferEndpoint>(repository);

        await endpoint.HandleAsync(new CancelTransferRequest(transferId, "capacity no longer needed", 6), Token);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal(
            (transferId, 6L, "capacity no longer needed", ActorId, Token),
            Assert.Single(repository.CancelCalls));
        Assert.Empty(repository.GetByIdCalls);
    }

    [Fact]
    public async Task Cancel_returns_409_with_current_state_for_version_conflict()
    {
        var transferId = Guid.NewGuid();
        var current = HandlerGroupBTestData.Transfer(transferId, version: 9);
        var repository = new RecordingTransferRepository
        {
            CancelResult = MutationOutcome.VersionConflict,
            GetByIdResult = current,
        };
        var endpoint = Create<CancelTransferEndpoint>(repository);

        await endpoint.HandleAsync(new CancelTransferRequest(transferId, "capacity no longer needed", 6), Token);

        Assert.Equal(409, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(current, endpoint.Response);
        Assert.Equal(
            (transferId, 6L, "capacity no longer needed", ActorId, Token),
            Assert.Single(repository.CancelCalls));
        Assert.Equal((transferId, Token), Assert.Single(repository.GetByIdCalls));
    }

    private static CreateTransferRequest CreateRequest() => new(
        Guid.NewGuid(),
        new DateOnly(2026, 2, 1),
        "NRGD.49.GAS.THE.TRF.MON-2-2026",
        Guid.NewGuid(),
        "GTF",
        "THE",
        10m,
        9m,
        216m,
        -2m,
        new DateOnly(2026, 2, 1),
        new DateOnly(2026, 2, 28),
        "TTF",
        0.5m,
        0.75m,
        "Awaiting",
        "fixture transfer");

    private static UpdateTransferRequest UpdateRequest(Guid transferId, long version) => new(
        transferId,
        "THE",
        11m,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        "updated transfer",
        version);

    private static TEndpoint Create<TEndpoint>(RecordingTransferRepository repository)
        where TEndpoint : BaseEndpoint =>
        Factory.Create<TEndpoint>(
            context => context.User = HandlerGroupBTestData.Principal(ActorId), repository);

    private static void AssertUpdateCall(
        RecordingTransferRepository repository,
        UpdateTransferRequest request)
    {
        var call = Assert.Single(repository.UpdateCalls);
        Assert.Same(request, call.Request);
        Assert.Equal(ActorId, call.ActorId);
        Assert.Equal(Token, call.Token);
    }
}
