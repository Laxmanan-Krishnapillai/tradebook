using Tradebook.Core.DTOs;

namespace Tradebook.Core.Interfaces;

public interface ITaxTariffRepository
{
    Task<TaxTariffDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<GetTaxTariffHistoryResponse> GetHistoryAsync(
        GetTaxTariffHistoryRequest request,
        CancellationToken ct
    );
    Task<TaxTariffDetailsDto> CreateAtomicAsync(
        CreateTaxTariffRequest request,
        Guid actorId,
        CancellationToken ct
    );
    Task<TaxTariffDetailsDto?> UpdateAtomicAsync(
        UpdateTaxTariffRequest request,
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
