using Tradebook.Core.DTOs;

namespace Tradebook.Core.Interfaces;

public interface ITransferRepository
{
    Task<TransferDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<GetTransferHistoryResponse> GetHistoryAsync(GetTransferHistoryRequest request, CancellationToken ct);
    Task<TransferDetailsDto> CreateAtomicAsync(CreateTransferRequest request, Guid actorId, CancellationToken ct);
    Task<TransferDetailsDto?> UpdateAtomicAsync(UpdateTransferRequest request, Guid actorId, CancellationToken ct);
    Task<MutationOutcome?> CancelAtomicAsync(Guid id, long version, string reason, Guid actorId, CancellationToken ct);
}
