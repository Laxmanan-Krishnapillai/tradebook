using Tradebook.Core.DTOs;

namespace Tradebook.Core.Interfaces;

public interface IHedgeRepository
{
    Task<HedgeDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<GetHedgeHistoryResponse> GetHistoryAsync(
        GetHedgeHistoryRequest request,
        CancellationToken ct
    );
    Task<HedgeDetailsDto> CreateAtomicAsync(
        CreateHedgeRequest request,
        Guid actorId,
        CancellationToken ct
    );
    Task<HedgeDetailsDto?> UpdateAtomicAsync(
        UpdateHedgeRequest request,
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
