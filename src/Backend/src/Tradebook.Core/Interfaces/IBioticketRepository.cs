using Tradebook.Core.DTOs;

namespace Tradebook.Core.Interfaces;

public interface IBioticketRepository
{
    Task<BioticketDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<GetBioticketHistoryResponse> GetHistoryAsync(GetBioticketHistoryRequest request, CancellationToken ct);
    Task<BioticketDetailsDto> CreateAtomicAsync(CreateBioticketRequest request, Guid actorId, CancellationToken ct);
    Task<BioticketDetailsDto?> UpdateAtomicAsync(UpdateBioticketRequest request, Guid actorId, CancellationToken ct);
    Task<MutationOutcome?> CancelAtomicAsync(Guid id, long version, string reason, Guid actorId, CancellationToken ct);
}
