using Tradebook.Core.DTOs;

namespace Tradebook.Core.Interfaces;

public interface IGooCertificateRepository
{
    Task<GooCertificateTransactionDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<GetGooCertificateHistoryResponse> GetHistoryAsync(GetGooCertificateHistoryRequest request, CancellationToken ct);
    Task<GooCertificateTransactionDetailsDto> CreateAtomicAsync(CreateGooCertificateTransactionRequest request, Guid actorId, CancellationToken ct);
    Task<GooCertificateTransactionDetailsDto?> UpdateAtomicAsync(UpdateGooCertificateTransactionRequest request, Guid actorId, CancellationToken ct);
    Task<GooCertificateTransactionDetailsDto?> RequestBatchExportAtomicAsync(Guid id, long version, Guid actorId, CancellationToken ct);
    Task<MutationOutcome?> DeleteAtomicAsync(Guid id, long version, string reason, Guid actorId, CancellationToken ct);
}
