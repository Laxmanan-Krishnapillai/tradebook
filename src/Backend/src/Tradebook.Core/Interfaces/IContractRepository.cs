using Tradebook.Core.DTOs;

namespace Tradebook.Core.Interfaces;

public interface IContractRepository
{
    Task<ContractDetailsDto?> GetByIdAsync(Guid contractId, CancellationToken ct);
    Task<GetContractHistoryResponse> GetHistoryAsync(GetContractHistoryRequest request, CancellationToken ct);
    Task<ContractDetailsDto> CreateAtomicAsync(CreateContractRequest request, Guid actorId, CancellationToken ct);
    Task<ContractDetailsDto?> UpdateAtomicAsync(UpdateContractRequest request, Guid actorId, CancellationToken ct);
    Task<MutationOutcome?> DeactivateAtomicAsync(Guid contractId, long version, string reason, Guid actorId, CancellationToken ct);
}
