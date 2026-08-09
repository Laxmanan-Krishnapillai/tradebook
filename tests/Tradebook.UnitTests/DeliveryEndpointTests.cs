using System.Security.Claims;
using FastEndpoints;
using Tradebook.Api.Features.PhysicalDeliveries.CreatePhysicalDelivery;
using Tradebook.Api.Features.PhysicalDeliveries.DeletePhysicalDelivery;
using Tradebook.Api.Features.PhysicalDeliveries.GetDeliveryById;
using Tradebook.Api.Features.PhysicalDeliveries.GetDeliveryHistory;
using Tradebook.Api.Features.PhysicalDeliveries.UpdatePhysicalDelivery;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

public sealed class DeliveryEndpointTests
{
    private static readonly Guid Actor = Guid.NewGuid();

    private static ClaimsPrincipal Principal() =>
        new(new ClaimsIdentity([new Claim("oid", Actor.ToString()), new("tid", "11111111-1111-1111-1111-111111111111"), new("tradebook_tenant", "11111111-1111-1111-1111-111111111111")], "test"));

    [Fact]
    public async Task Create_returns_201_with_mapped_response_and_evicts_list_cache()
    {
        var repository = new FakeDeliveryRepository { CreateResult = TestData.Delivery() };
        var cache = new FakeCacheService();
        var endpoint = Factory.Create<CreatePhysicalDeliveryEndpoint>(ctx => ctx.User = Principal(), repository, cache);
        var request = new CreatePhysicalDeliveryRequest(
            Guid.NewGuid(), "TEST45.SG.2601.NOQS-1-2026", "Sales", new DateOnly(2026, 1, 1),
            null, 10m, 9m, "TTF", null, null);

        await endpoint.HandleAsync(request, default);

        Assert.Equal(201, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal(repository.CreateResult.DeliveryId, endpoint.Response.DeliveryId);
        Assert.Equal(repository.CreateResult.ContractInstanceId, endpoint.Response.ContractInstanceId);
        Assert.Equal(repository.CreateResult.InvoiceAmountEur, endpoint.Response.InvoiceAmountEur);
        Assert.Equal(repository.CreateResult.Status, endpoint.Response.Status);
        Assert.Equal(repository.CreateResult.Version, endpoint.Response.Version);
        Assert.Same(request, repository.LastCreateRequest);
        Assert.Equal(Actor, repository.LastActorId);
        Assert.Equal(["deliveries:list"], cache.RemovedKeys);
    }

    [Fact]
    public async Task Update_returns_200_and_evicts_both_cache_keys()
    {
        var deliveryId = Guid.NewGuid();
        var repository = new FakeDeliveryRepository { UpdateResult = TestData.Delivery(deliveryId, version: 2) };
        var cache = new FakeCacheService();
        var endpoint = Factory.Create<UpdatePhysicalDeliveryEndpoint>(ctx => ctx.User = Principal(), repository, cache);
        var request = new UpdatePhysicalDeliveryRequest(deliveryId, 11m, null, 1);

        await endpoint.HandleAsync(request, default);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(repository.UpdateResult, endpoint.Response);
        Assert.Same(request, repository.LastUpdateRequest);
        Assert.Equal(Actor, repository.LastActorId);
        Assert.Equal([$"delivery:{deliveryId}", "deliveries:list"], cache.RemovedKeys);
    }

    [Fact]
    public async Task Update_version_conflict_returns_409_with_current_state_and_keeps_cache()
    {
        var deliveryId = Guid.NewGuid();
        var repository = new FakeDeliveryRepository { UpdateResult = null, GetByIdResult = TestData.Delivery(deliveryId, version: 5) };
        var cache = new FakeCacheService();
        var endpoint = Factory.Create<UpdatePhysicalDeliveryEndpoint>(ctx => ctx.User = Principal(), repository, cache);

        await endpoint.HandleAsync(new UpdatePhysicalDeliveryRequest(deliveryId, 11m, null, 1), default);

        Assert.Equal(409, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(repository.GetByIdResult, endpoint.Response);
        Assert.Empty(cache.RemovedKeys);
    }

    [Fact]
    public async Task Update_of_missing_delivery_returns_404()
    {
        var repository = new FakeDeliveryRepository { UpdateResult = null, GetByIdResult = null };
        var cache = new FakeCacheService();
        var endpoint = Factory.Create<UpdatePhysicalDeliveryEndpoint>(ctx => ctx.User = Principal(), repository, cache);

        await endpoint.HandleAsync(new UpdatePhysicalDeliveryRequest(Guid.NewGuid(), 11m, null, 1), default);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        Assert.Empty(cache.RemovedKeys);
    }

    [Fact]
    public async Task Delete_returns_204_and_evicts_both_cache_keys()
    {
        var deliveryId = Guid.NewGuid();
        var repository = new FakeDeliveryRepository { CancelOutcome = null };
        var cache = new FakeCacheService();
        var endpoint = Factory.Create<DeletePhysicalDeliveryEndpoint>(ctx => ctx.User = Principal(), repository, cache);

        await endpoint.HandleAsync(new DeletePhysicalDeliveryRequest(deliveryId, "Duplicate entry", 3), default);

        Assert.Equal(204, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal((deliveryId, 3L, "Duplicate entry", Actor), repository.LastCancel);
        Assert.Equal([$"delivery:{deliveryId}", "deliveries:list"], cache.RemovedKeys);
    }

    [Fact]
    public async Task Delete_of_missing_delivery_returns_404()
    {
        var repository = new FakeDeliveryRepository { CancelOutcome = MutationOutcome.NotFound };
        var cache = new FakeCacheService();
        var endpoint = Factory.Create<DeletePhysicalDeliveryEndpoint>(ctx => ctx.User = Principal(), repository, cache);

        await endpoint.HandleAsync(new DeletePhysicalDeliveryRequest(Guid.NewGuid(), "Duplicate entry", 3), default);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal(0, repository.GetByIdCalls);
        Assert.Empty(cache.RemovedKeys);
    }

    [Fact]
    public async Task Delete_version_conflict_returns_409_with_current_state()
    {
        var deliveryId = Guid.NewGuid();
        var repository = new FakeDeliveryRepository { CancelOutcome = MutationOutcome.VersionConflict, GetByIdResult = TestData.Delivery(deliveryId, version: 7) };
        var cache = new FakeCacheService();
        var endpoint = Factory.Create<DeletePhysicalDeliveryEndpoint>(ctx => ctx.User = Principal(), repository, cache);

        await endpoint.HandleAsync(new DeletePhysicalDeliveryRequest(deliveryId, "Duplicate entry", 1), default);

        Assert.Equal(409, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(repository.GetByIdResult, endpoint.Response);
        Assert.Empty(cache.RemovedKeys);
    }

    [Fact]
    public async Task GetById_reads_through_cache_with_exact_key_and_returns_200()
    {
        var deliveryId = Guid.NewGuid();
        var repository = new FakeDeliveryRepository { GetByIdResult = TestData.Delivery(deliveryId) };
        var cache = new FakeCacheService();
        var endpoint = Factory.Create<GetDeliveryByIdEndpoint>(ctx => ctx.User = Principal(), repository, cache);

        await endpoint.HandleAsync(new GetDeliveryByIdRequest(deliveryId), default);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(repository.GetByIdResult, endpoint.Response);
        Assert.Equal([$"delivery:{deliveryId}"], cache.RequestedKeys);
        Assert.Equal(1, repository.GetByIdCalls);
    }

    [Fact]
    public async Task GetById_returns_404_when_absent()
    {
        var repository = new FakeDeliveryRepository { GetByIdResult = null };
        var endpoint = Factory.Create<GetDeliveryByIdEndpoint>(ctx => ctx.User = Principal(), repository, new FakeCacheService());

        await endpoint.HandleAsync(new GetDeliveryByIdRequest(Guid.NewGuid()), default);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
    }

    [Fact]
    public async Task History_forwards_request_and_returns_repository_response()
    {
        var repository = new FakeDeliveryRepository
        {
            HistoryResult = new([TestData.Delivery()], 1, 2, 25, false),
        };
        var endpoint = Factory.Create<GetDeliveryHistoryEndpoint>(ctx => ctx.User = Principal(), repository);
        var request = new GetDeliveryHistoryRequest(null, null, "Sales", null, null, null, 2, 25);

        await endpoint.HandleAsync(request, default);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(repository.HistoryResult, endpoint.Response);
        Assert.Same(request, repository.LastHistoryRequest);
    }
}
