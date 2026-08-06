using Tradebook.Core.Domain.Entities;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

public sealed class FakeDeliveryRepository : IDeliveryRepository
{
    public PhysicalDeliveryDetailsDto? CreateResult { get; set; }
    public PhysicalDeliveryDetailsDto? UpdateResult { get; set; }
    public PhysicalDeliveryDetailsDto? GetByIdResult { get; set; }
    public MutationOutcome? CancelOutcome { get; set; }
    public GetDeliveryHistoryResponse HistoryResult { get; set; } = new([], 0, 1, 50, false);

    public CreatePhysicalDeliveryRequest? LastCreateRequest { get; private set; }
    public UpdatePhysicalDeliveryRequest? LastUpdateRequest { get; private set; }
    public GetDeliveryHistoryRequest? LastHistoryRequest { get; private set; }
    public (Guid DeliveryId, long Version, string Reason, Guid ActorId)? LastCancel { get; private set; }
    public Guid LastActorId { get; private set; }
    public int GetByIdCalls { get; private set; }

    public Task<PhysicalDeliveryDetailsDto?> GetByIdAsync(Guid deliveryId, CancellationToken cancellationToken)
    {
        GetByIdCalls++;
        return Task.FromResult(GetByIdResult);
    }

    public Task<GetDeliveryHistoryResponse> GetHistoryAsync(GetDeliveryHistoryRequest request, CancellationToken cancellationToken)
    {
        LastHistoryRequest = request;
        return Task.FromResult(HistoryResult);
    }

    public Task<PhysicalDeliveryDetailsDto> CreateAtomicAsync(CreatePhysicalDeliveryRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        LastCreateRequest = request;
        LastActorId = actorId;
        return Task.FromResult(CreateResult!);
    }

    public Task<PhysicalDeliveryDetailsDto?> UpdateAtomicAsync(UpdatePhysicalDeliveryRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        LastUpdateRequest = request;
        LastActorId = actorId;
        return Task.FromResult(UpdateResult);
    }

    public Task<MutationOutcome?> CancelAtomicAsync(Guid deliveryId, long expectedVersion, string reason, Guid actorId, CancellationToken cancellationToken)
    {
        LastCancel = (deliveryId, expectedVersion, reason, actorId);
        LastActorId = actorId;
        return Task.FromResult(CancelOutcome);
    }
}

public sealed class FakeCacheService : ICacheService
{
    public List<string> RequestedKeys { get; } = [];
    public List<string> RemovedKeys { get; } = [];

    public async ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, TimeSpan expiration, CancellationToken cancellationToken)
    {
        RequestedKeys.Add(key);
        return await factory(cancellationToken);
    }

    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken)
    {
        RemovedKeys.Add(key);
        return ValueTask.CompletedTask;
    }
}

public sealed class FakeUserRepository : IUserRepository
{
    public Dictionary<string, User> Users { get; } = [];

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
        Task.FromResult(Users.TryGetValue(username, out var user) ? user : null);
}

public static class TestData
{
    public static PhysicalDeliveryDetailsDto Delivery(Guid? deliveryId = null, long version = 1) => new(
        deliveryId ?? Guid.NewGuid(), Guid.NewGuid(), "TEST45.SG.2601.NOQS-1-2026", "Sales",
        new DateOnly(2026, 1, 1), null, 10m, 9m, 9m, "TTF", 100m, 100m, 25m, 125m,
        "Awaiting", version, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}
