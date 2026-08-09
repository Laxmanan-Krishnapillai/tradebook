using FastEndpoints;
using Tradebook.Api.Features.TaxTariffs;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

public sealed class HandlerGroupBTaxTariffEndpointTests : IDisposable
{
    private static readonly Guid ActorId = Guid.NewGuid();
    private readonly CancellationTokenSource tokenSource = new();
    private CancellationToken Token => tokenSource.Token;

    public void Dispose() => tokenSource.Dispose();

    [Fact]
    public async Task CreateReturns201AndForwardsTheExactRequestActorAndToken()
    {
        var expected = HandlerGroupBTestData.TaxTariff(version: 1);
        var repository = new RecordingTaxTariffRepository { CreateResult = expected };
        var endpoint = Create<CreateTaxTariffEndpoint>(repository);
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
        var tariffId = Guid.NewGuid();
        var expected = HandlerGroupBTestData.TaxTariff(tariffId);
        var repository = new RecordingTaxTariffRepository { GetByIdResult = expected };
        var endpoint = Create<GetTaxTariffByIdEndpoint>(repository);

        await endpoint.HandleAsync(new GetTaxTariffByIdRequest(tariffId), Token);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(expected, endpoint.Response);
        Assert.Equal((tariffId, Token), Assert.Single(repository.GetByIdCalls));
    }

    [Fact]
    public async Task GetByIdReturns404WhenTheRepositoryHasNoRow()
    {
        var tariffId = Guid.NewGuid();
        var repository = new RecordingTaxTariffRepository { GetByIdResult = null };
        var endpoint = Create<GetTaxTariffByIdEndpoint>(repository);

        await endpoint.HandleAsync(new GetTaxTariffByIdRequest(tariffId), Token);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal((tariffId, Token), Assert.Single(repository.GetByIdCalls));
    }

    [Fact]
    public async Task HistoryReturns200AndForwardsTheExactRequestAndToken()
    {
        var expected = new GetTaxTariffHistoryResponse(
            [HandlerGroupBTestData.TaxTariff()],
            12,
            2,
            25,
            true
        );
        var repository = new RecordingTaxTariffRepository { HistoryResult = expected };
        var endpoint = Create<GetTaxTariffHistoryEndpoint>(repository);
        var request = new GetTaxTariffHistoryRequest(
            Guid.NewGuid(),
            new DateOnly(2026, 6, 1),
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
        var tariffId = Guid.NewGuid();
        var expected = HandlerGroupBTestData.TaxTariff(tariffId, version: 4);
        var repository = new RecordingTaxTariffRepository { UpdateResult = expected };
        var endpoint = Create<UpdateTaxTariffEndpoint>(repository);
        var request = UpdateRequest(tariffId, version: 3);

        await endpoint.HandleAsync(request, Token);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(expected, endpoint.Response);
        AssertUpdateCall(repository, request);
        Assert.Empty(repository.GetByIdCalls);
    }

    [Fact]
    public async Task UpdateReturns404AfterAFailedUpdateAndMissingLookup()
    {
        var tariffId = Guid.NewGuid();
        var repository = new RecordingTaxTariffRepository
        {
            UpdateResult = null,
            GetByIdResult = null,
        };
        var endpoint = Create<UpdateTaxTariffEndpoint>(repository);
        var request = UpdateRequest(tariffId, version: 3);

        await endpoint.HandleAsync(request, Token);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        AssertUpdateCall(repository, request);
        Assert.Equal((tariffId, Token), Assert.Single(repository.GetByIdCalls));
    }

    [Fact]
    public async Task UpdateReturns409WithCurrentStateAfterAFailedUpdate()
    {
        var tariffId = Guid.NewGuid();
        var current = HandlerGroupBTestData.TaxTariff(tariffId, version: 8);
        var repository = new RecordingTaxTariffRepository
        {
            UpdateResult = null,
            GetByIdResult = current,
        };
        var endpoint = Create<UpdateTaxTariffEndpoint>(repository);
        var request = UpdateRequest(tariffId, version: 3);

        await endpoint.HandleAsync(request, Token);

        Assert.Equal(409, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(current, endpoint.Response);
        AssertUpdateCall(repository, request);
        Assert.Equal((tariffId, Token), Assert.Single(repository.GetByIdCalls));
    }

    [Fact]
    public async Task DeleteReturns204AndForwardsEveryRepositoryArgument()
    {
        var tariffId = Guid.NewGuid();
        var repository = new RecordingTaxTariffRepository { DeleteResult = null };
        var endpoint = Create<DeleteTaxTariffEndpoint>(repository);

        await endpoint.HandleAsync(
            new DeleteTaxTariffRequest(tariffId, "superseded tariff", 6),
            Token
        );

        Assert.Equal(204, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal(
            (tariffId, 6L, "superseded tariff", ActorId, Token),
            Assert.Single(repository.DeleteCalls)
        );
        Assert.Empty(repository.GetByIdCalls);
    }

    [Fact]
    public async Task DeleteReturns404WithoutLoadingCurrentStateForNotFound()
    {
        var tariffId = Guid.NewGuid();
        var repository = new RecordingTaxTariffRepository
        {
            DeleteResult = MutationOutcome.NotFound,
        };
        var endpoint = Create<DeleteTaxTariffEndpoint>(repository);

        await endpoint.HandleAsync(
            new DeleteTaxTariffRequest(tariffId, "superseded tariff", 6),
            Token
        );

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal(
            (tariffId, 6L, "superseded tariff", ActorId, Token),
            Assert.Single(repository.DeleteCalls)
        );
        Assert.Empty(repository.GetByIdCalls);
    }

    [Fact]
    public async Task DeleteReturns409WithCurrentStateForVersionConflict()
    {
        var tariffId = Guid.NewGuid();
        var current = HandlerGroupBTestData.TaxTariff(tariffId, version: 9);
        var repository = new RecordingTaxTariffRepository
        {
            DeleteResult = MutationOutcome.VersionConflict,
            GetByIdResult = current,
        };
        var endpoint = Create<DeleteTaxTariffEndpoint>(repository);

        await endpoint.HandleAsync(
            new DeleteTaxTariffRequest(tariffId, "superseded tariff", 6),
            Token
        );

        Assert.Equal(409, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(current, endpoint.Response);
        Assert.Equal(
            (tariffId, 6L, "superseded tariff", ActorId, Token),
            Assert.Single(repository.DeleteCalls)
        );
        Assert.Equal((tariffId, Token), Assert.Single(repository.GetByIdCalls));
    }

    private static CreateTaxTariffRequest CreateRequest() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            1.1m,
            2.2m,
            3.3m,
            4.4m,
            5.5m,
            6.6m,
            "DKK"
        );

    private static UpdateTaxTariffRequest UpdateRequest(Guid tariffId, long version) =>
        new(tariffId, 1.2m, null, null, null, null, null, "DKK", version);

    private static TEndpoint Create<TEndpoint>(RecordingTaxTariffRepository repository)
        where TEndpoint : BaseEndpoint =>
        Factory.Create<TEndpoint>(
            context => context.User = HandlerGroupBTestData.Principal(ActorId),
            repository
        );

    private void AssertUpdateCall(
        RecordingTaxTariffRepository repository,
        UpdateTaxTariffRequest request
    )
    {
        var call = Assert.Single(repository.UpdateCalls);
        Assert.Same(request, call.Request);
        Assert.Equal(ActorId, call.ActorId);
        Assert.Equal(Token, call.Token);
    }
}
