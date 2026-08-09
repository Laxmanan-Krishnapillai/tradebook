using System.Security.Claims;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

internal sealed class RecordingTaxTariffRepository : ITaxTariffRepository
{
    public TaxTariffDetailsDto CreateResult { get; set; } = HandlerGroupBTestData.TaxTariff();
    public TaxTariffDetailsDto? UpdateResult { get; set; }
    public TaxTariffDetailsDto? GetByIdResult { get; set; }
    public MutationOutcome? DeleteResult { get; set; }
    public GetTaxTariffHistoryResponse HistoryResult { get; set; } =
        new([HandlerGroupBTestData.TaxTariff()], 1, 1, 50, false);

    public List<(Guid Id, CancellationToken Token)> GetByIdCalls { get; } = [];
    public List<(
        GetTaxTariffHistoryRequest Request,
        CancellationToken Token
    )> HistoryCalls { get; } = [];
    public List<(
        CreateTaxTariffRequest Request,
        Guid ActorId,
        CancellationToken Token
    )> CreateCalls { get; } = [];
    public List<(
        UpdateTaxTariffRequest Request,
        Guid ActorId,
        CancellationToken Token
    )> UpdateCalls { get; } = [];
    public List<(
        Guid Id,
        long Version,
        string Reason,
        Guid ActorId,
        CancellationToken Token
    )> DeleteCalls { get; } = [];

    public Task<TaxTariffDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        GetByIdCalls.Add((id, ct));
        return Task.FromResult(GetByIdResult);
    }

    public Task<GetTaxTariffHistoryResponse> GetHistoryAsync(
        GetTaxTariffHistoryRequest request,
        CancellationToken ct
    )
    {
        HistoryCalls.Add((request, ct));
        return Task.FromResult(HistoryResult);
    }

    public Task<TaxTariffDetailsDto> CreateAtomicAsync(
        CreateTaxTariffRequest request,
        Guid actorId,
        CancellationToken ct
    )
    {
        CreateCalls.Add((request, actorId, ct));
        return Task.FromResult(CreateResult);
    }

    public Task<TaxTariffDetailsDto?> UpdateAtomicAsync(
        UpdateTaxTariffRequest request,
        Guid actorId,
        CancellationToken ct
    )
    {
        UpdateCalls.Add((request, actorId, ct));
        return Task.FromResult(UpdateResult);
    }

    public Task<MutationOutcome?> DeleteAtomicAsync(
        Guid id,
        long version,
        string reason,
        Guid actorId,
        CancellationToken ct
    )
    {
        DeleteCalls.Add((id, version, reason, actorId, ct));
        return Task.FromResult(DeleteResult);
    }
}
