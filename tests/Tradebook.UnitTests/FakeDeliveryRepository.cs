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
    public (Guid DeliveryId, long Version, string Reason, Guid ActorId)? LastCancel
    {
        get;
        private set;
    }
    public Guid LastActorId { get; private set; }
    public int GetByIdCalls { get; private set; }

    public Task<PhysicalDeliveryDetailsDto?> GetByIdAsync(
        Guid deliveryId,
        CancellationToken cancellationToken
    )
    {
        GetByIdCalls++;
        return Task.FromResult(GetByIdResult);
    }

    public Task<GetDeliveryHistoryResponse> GetHistoryAsync(
        GetDeliveryHistoryRequest request,
        CancellationToken cancellationToken
    )
    {
        LastHistoryRequest = request;
        return Task.FromResult(HistoryResult);
    }

    public Task<PhysicalDeliveryDetailsDto> CreateAtomicAsync(
        CreatePhysicalDeliveryRequest request,
        Guid actorId,
        CancellationToken cancellationToken
    )
    {
        LastCreateRequest = request;
        LastActorId = actorId;
        return Task.FromResult(CreateResult!);
    }

    public Task<PhysicalDeliveryDetailsDto?> UpdateAtomicAsync(
        UpdatePhysicalDeliveryRequest request,
        Guid actorId,
        CancellationToken cancellationToken
    )
    {
        LastUpdateRequest = request;
        LastActorId = actorId;
        return Task.FromResult(UpdateResult);
    }

    public Task<MutationOutcome?> CancelAtomicAsync(
        Guid deliveryId,
        long expectedVersion,
        string reason,
        Guid actorId,
        CancellationToken cancellationToken
    )
    {
        LastCancel = (deliveryId, expectedVersion, reason, actorId);
        LastActorId = actorId;
        return Task.FromResult(CancelOutcome);
    }
}
