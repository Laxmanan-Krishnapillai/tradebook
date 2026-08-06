using Tradebook.Core.DTOs;

namespace Tradebook.Core.Interfaces;

public enum MutationOutcome { NotFound, VersionConflict }

public interface IDeliveryRepository
{
    Task<PhysicalDeliveryDetailsDto?> GetByIdAsync(Guid deliveryId, CancellationToken cancellationToken);
    Task<GetDeliveryHistoryResponse> GetHistoryAsync(GetDeliveryHistoryRequest request, CancellationToken cancellationToken);
    Task<PhysicalDeliveryDetailsDto> CreateAtomicAsync(CreatePhysicalDeliveryRequest request, Guid actorId, CancellationToken cancellationToken);
    Task<PhysicalDeliveryDetailsDto?> UpdateAtomicAsync(UpdatePhysicalDeliveryRequest request, Guid actorId, CancellationToken cancellationToken);
    Task<MutationOutcome?> CancelAtomicAsync(Guid deliveryId, long expectedVersion, string reason, Guid actorId, CancellationToken cancellationToken);
}
