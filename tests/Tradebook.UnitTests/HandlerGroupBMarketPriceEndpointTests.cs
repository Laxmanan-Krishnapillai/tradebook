using FastEndpoints;
using Tradebook.Api.Features.MarketPrices;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

public sealed class HandlerGroupBMarketPriceEndpointTests
{
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly CancellationTokenSource TokenSource = new();
    private static readonly DateOnly PriceDate = new(2026, 2, 3);
    private static CancellationToken Token => TokenSource.Token;

    [Fact]
    public async Task Upsert_returns_200_and_forwards_the_exact_request_actor_and_token()
    {
        var expected = HandlerGroupBTestData.MarketPrice(PriceDate, version: 1);
        var repository = new RecordingMarketPriceRepository { UpsertResult = expected };
        var endpoint = Create<UpsertMarketPriceEndpoint>(repository);
        var request = Upsert(version: 0);

        await endpoint.HandleAsync(request, Token);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(expected, endpoint.Response);
        AssertUpsertCall(repository, request);
        Assert.Empty(repository.GetByDateCalls);
    }

    [Fact]
    public async Task Upsert_returns_404_after_a_failed_write_and_missing_lookup()
    {
        var repository = new RecordingMarketPriceRepository { UpsertResult = null, GetByDateResult = null };
        var endpoint = Create<UpsertMarketPriceEndpoint>(repository);
        var request = Upsert(version: 4);

        await endpoint.HandleAsync(request, Token);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        AssertUpsertCall(repository, request);
        Assert.Equal((PriceDate, Token), Assert.Single(repository.GetByDateCalls));
    }

    [Fact]
    public async Task Upsert_returns_409_with_current_state_after_a_failed_write()
    {
        var current = HandlerGroupBTestData.MarketPrice(PriceDate, version: 8);
        var repository = new RecordingMarketPriceRepository
        {
            UpsertResult = null,
            GetByDateResult = current,
        };
        var endpoint = Create<UpsertMarketPriceEndpoint>(repository);
        var request = Upsert(version: 4);

        await endpoint.HandleAsync(request, Token);

        Assert.Equal(409, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(current, endpoint.Response);
        AssertUpsertCall(repository, request);
        Assert.Equal((PriceDate, Token), Assert.Single(repository.GetByDateCalls));
    }

    [Fact]
    public async Task GetByDate_returns_200_with_the_repository_result_and_exact_call()
    {
        var expected = HandlerGroupBTestData.MarketPrice(PriceDate);
        var repository = new RecordingMarketPriceRepository { GetByDateResult = expected };
        var endpoint = Create<GetMarketPriceByDateEndpoint>(repository);

        await endpoint.HandleAsync(new GetMarketPriceByDateRequest(PriceDate), Token);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(expected, endpoint.Response);
        Assert.Equal((PriceDate, Token), Assert.Single(repository.GetByDateCalls));
    }

    [Fact]
    public async Task GetByDate_returns_404_when_the_repository_has_no_row()
    {
        var repository = new RecordingMarketPriceRepository { GetByDateResult = null };
        var endpoint = Create<GetMarketPriceByDateEndpoint>(repository);

        await endpoint.HandleAsync(new GetMarketPriceByDateRequest(PriceDate), Token);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal((PriceDate, Token), Assert.Single(repository.GetByDateCalls));
    }

    [Fact]
    public async Task History_returns_200_and_forwards_the_exact_request_and_token()
    {
        var expected = new GetMarketPriceHistoryResponse(
            [HandlerGroupBTestData.MarketPrice(PriceDate)], 9, 3, 40, true);
        var repository = new RecordingMarketPriceRepository { HistoryResult = expected };
        var endpoint = Create<GetMarketPriceHistoryEndpoint>(repository);
        var request = new GetMarketPriceHistoryRequest(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31), 3, 40);

        await endpoint.HandleAsync(request, Token);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(expected, endpoint.Response);
        var call = Assert.Single(repository.HistoryCalls);
        Assert.Same(request, call.Request);
        Assert.Equal(Token, call.Token);
    }

    [Fact]
    public async Task Delete_returns_204_and_forwards_every_repository_argument()
    {
        var repository = new RecordingMarketPriceRepository { DeleteResult = null };
        var endpoint = Create<DeleteMarketPriceEndpoint>(repository);
        var request = new DeleteMarketPriceRequest(PriceDate, "bad source data", 6);

        await endpoint.HandleAsync(request, Token);

        Assert.Equal(204, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal((PriceDate, 6L, "bad source data", ActorId, Token), Assert.Single(repository.DeleteCalls));
        Assert.Empty(repository.GetByDateCalls);
    }

    [Fact]
    public async Task Delete_returns_404_without_loading_current_state_for_not_found()
    {
        var repository = new RecordingMarketPriceRepository { DeleteResult = MutationOutcome.NotFound };
        var endpoint = Create<DeleteMarketPriceEndpoint>(repository);

        await endpoint.HandleAsync(new DeleteMarketPriceRequest(PriceDate, "bad source data", 6), Token);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal((PriceDate, 6L, "bad source data", ActorId, Token), Assert.Single(repository.DeleteCalls));
        Assert.Empty(repository.GetByDateCalls);
    }

    [Fact]
    public async Task Delete_returns_409_with_current_state_for_version_conflict()
    {
        var current = HandlerGroupBTestData.MarketPrice(PriceDate, version: 9);
        var repository = new RecordingMarketPriceRepository
        {
            DeleteResult = MutationOutcome.VersionConflict,
            GetByDateResult = current,
        };
        var endpoint = Create<DeleteMarketPriceEndpoint>(repository);

        await endpoint.HandleAsync(new DeleteMarketPriceRequest(PriceDate, "bad source data", 6), Token);

        Assert.Equal(409, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(current, endpoint.Response);
        Assert.Equal((PriceDate, 6L, "bad source data", ActorId, Token), Assert.Single(repository.DeleteCalls));
        Assert.Equal((PriceDate, Token), Assert.Single(repository.GetByDateCalls));
    }

    private static UpsertMarketPriceRequest Upsert(long version) => new(
        PriceDate,
        31.75m,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        version);

    private static TEndpoint Create<TEndpoint>(RecordingMarketPriceRepository repository)
        where TEndpoint : BaseEndpoint =>
        Factory.Create<TEndpoint>(
            context => context.User = HandlerGroupBTestData.Principal(ActorId), repository);

    private static void AssertUpsertCall(
        RecordingMarketPriceRepository repository,
        UpsertMarketPriceRequest request)
    {
        var call = Assert.Single(repository.UpsertCalls);
        Assert.Same(request, call.Request);
        Assert.Equal(ActorId, call.ActorId);
        Assert.Equal(Token, call.Token);
    }
}
