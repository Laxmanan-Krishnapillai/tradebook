using FastEndpoints;
using Tradebook.Api.Features.Transfers;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

public sealed class HandlerGroupBTransferEndpointTests : IDisposable
{
    private static readonly Guid ActorId = Guid.NewGuid();
    private readonly CancellationTokenSource tokenSource = new();
    private CancellationToken Token => tokenSource.Token;

    public void Dispose() => tokenSource.Dispose();

    [Fact]
    public async Task CreateReturns201AndForwardsTheExactRequestActorAndToken()
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
    public async Task GetByIdReturns200WithTheRepositoryResultAndExactCall()
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
    public async Task GetByIdReturns404WhenTheRepositoryHasNoRow()
    {
        var transferId = Guid.NewGuid();
        var repository = new RecordingTransferRepository { GetByIdResult = null };
        var endpoint = Create<GetTransferByIdEndpoint>(repository);

        await endpoint.HandleAsync(new GetTransferByIdRequest(transferId), Token);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal((transferId, Token), Assert.Single(repository.GetByIdCalls));
    }

    [Fact]
    public async Task HistoryReturns200AndForwardsTheExactRequestAndToken()
    {
        var expected = new GetTransferHistoryResponse(
            [HandlerGroupBTestData.Transfer()],
            14,
            2,
            25,
            true
        );
        var repository = new RecordingTransferRepository { HistoryResult = expected };
        var endpoint = Create<GetTransferHistoryEndpoint>(repository);
        var request = new GetTransferHistoryRequest(
            Guid.NewGuid(),
            "Awaiting",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 3, 1),
            2,
            25
        );

        await endpoint.HandleAsync(request, Token);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(expected, endpoint.Response);
        var call = Assert.Single(repository.HistoryCalls);
        Assert.Same(request, call.Request);
        Assert.Equal(Token, call.Token);
    }

    [Fact]
    public async Task UpdateReturns200AndDoesNotIssueAConflictLookup()
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
    public async Task UpdateReturns404AfterAFailedUpdateAndMissingLookup()
    {
        var transferId = Guid.NewGuid();
        var repository = new RecordingTransferRepository
        {
            UpdateResult = null,
            GetByIdResult = null,
        };
        var endpoint = Create<UpdateTransferEndpoint>(repository);
        var request = UpdateRequest(transferId, version: 3);

        await endpoint.HandleAsync(request, Token);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        AssertUpdateCall(repository, request);
        Assert.Equal((transferId, Token), Assert.Single(repository.GetByIdCalls));
    }

    [Fact]
    public async Task UpdateReturns409WithCurrentStateAfterAFailedUpdate()
    {
        var transferId = Guid.NewGuid();
        var current = HandlerGroupBTestData.Transfer(transferId, version: 8);
        var repository = new RecordingTransferRepository
        {
            UpdateResult = null,
            GetByIdResult = current,
        };
        var endpoint = Create<UpdateTransferEndpoint>(repository);
        var request = UpdateRequest(transferId, version: 3);

        await endpoint.HandleAsync(request, Token);

        Assert.Equal(409, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(current, endpoint.Response);
        AssertUpdateCall(repository, request);
        Assert.Equal((transferId, Token), Assert.Single(repository.GetByIdCalls));
    }

    [Fact]
    public async Task CancelReturns204AndForwardsEveryRepositoryArgument()
    {
        var transferId = Guid.NewGuid();
        var repository = new RecordingTransferRepository { CancelResult = null };
        var endpoint = Create<CancelTransferEndpoint>(repository);

        await endpoint.HandleAsync(
            new CancelTransferRequest(transferId, "capacity no longer needed", 6),
            Token
        );

        Assert.Equal(204, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal(
            (transferId, 6L, "capacity no longer needed", ActorId, Token),
            Assert.Single(repository.CancelCalls)
        );
        Assert.Empty(repository.GetByIdCalls);
    }

    [Fact]
    public async Task CancelReturns404WithoutLoadingCurrentStateForNotFound()
    {
        var transferId = Guid.NewGuid();
        var repository = new RecordingTransferRepository
        {
            CancelResult = MutationOutcome.NotFound,
        };
        var endpoint = Create<CancelTransferEndpoint>(repository);

        await endpoint.HandleAsync(
            new CancelTransferRequest(transferId, "capacity no longer needed", 6),
            Token
        );

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal(
            (transferId, 6L, "capacity no longer needed", ActorId, Token),
            Assert.Single(repository.CancelCalls)
        );
        Assert.Empty(repository.GetByIdCalls);
    }

    [Fact]
    public async Task CancelReturns409WithCurrentStateForVersionConflict()
    {
        var transferId = Guid.NewGuid();
        var current = HandlerGroupBTestData.Transfer(transferId, version: 9);
        var repository = new RecordingTransferRepository
        {
            CancelResult = MutationOutcome.VersionConflict,
            GetByIdResult = current,
        };
        var endpoint = Create<CancelTransferEndpoint>(repository);

        await endpoint.HandleAsync(
            new CancelTransferRequest(transferId, "capacity no longer needed", 6),
            Token
        );

        Assert.Equal(409, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(current, endpoint.Response);
        Assert.Equal(
            (transferId, 6L, "capacity no longer needed", ActorId, Token),
            Assert.Single(repository.CancelCalls)
        );
        Assert.Equal((transferId, Token), Assert.Single(repository.GetByIdCalls));
    }

    private static CreateTransferRequest CreateRequest() =>
        new(
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
            "fixture transfer"
        );

    private static UpdateTransferRequest UpdateRequest(Guid transferId, long version) =>
        new(
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
            version
        );

    private static TEndpoint Create<TEndpoint>(RecordingTransferRepository repository)
        where TEndpoint : BaseEndpoint =>
        Factory.Create<TEndpoint>(
            context => context.User = HandlerGroupBTestData.Principal(ActorId),
            repository
        );

    private void AssertUpdateCall(
        RecordingTransferRepository repository,
        UpdateTransferRequest request
    )
    {
        var call = Assert.Single(repository.UpdateCalls);
        Assert.Same(request, call.Request);
        Assert.Equal(ActorId, call.ActorId);
        Assert.Equal(Token, call.Token);
    }
}
