using Tradebook.Core.DTOs;

namespace Tradebook.Core.Interfaces;

public interface ICapacityBookingRepository
{
    Task<CapacityBookingDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<GetCapacityBookingHistoryResponse> GetHistoryAsync(
        GetCapacityBookingHistoryRequest request,
        CancellationToken ct
    );
    Task<CapacityBookingDetailsDto> CreateAtomicAsync(
        CreateCapacityBookingRequest request,
        Guid actorId,
        CancellationToken ct
    );
    Task<CapacityBookingDetailsDto?> UpdateAtomicAsync(
        UpdateCapacityBookingRequest request,
        Guid actorId,
        CancellationToken ct
    );
    Task<MutationOutcome?> DeleteAtomicAsync(
        Guid id,
        long version,
        string reason,
        Guid actorId,
        CancellationToken ct
    );
}
