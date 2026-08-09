using System.Security.Claims;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

internal sealed class FakeContractEndpointRepository : IContractRepository
{
    public ContractDetailsDto CreateResult { get; set; } = DomainEndpointTestData.Contract();
    public ContractDetailsDto? UpdateResult { get; set; }
    public ContractDetailsDto? GetByIdResult { get; set; }
    public MutationOutcome? DeactivateResult { get; set; }
    public GetContractHistoryResponse HistoryResult { get; set; } =
        new([DomainEndpointTestData.Contract()], 1, 1, 50, false);

    public List<(Guid Id, CancellationToken Token)> GetByIdCalls { get; } = [];
    public List<(
        GetContractHistoryRequest Request,
        CancellationToken Token
    )> HistoryCalls { get; } = [];
    public List<(
        CreateContractRequest Request,
        Guid ActorId,
        CancellationToken Token
    )> CreateCalls { get; } = [];
    public List<(
        UpdateContractRequest Request,
        Guid ActorId,
        CancellationToken Token
    )> UpdateCalls { get; } = [];
    public List<(
        Guid Id,
        long Version,
        string Reason,
        Guid ActorId,
        CancellationToken Token
    )> DeactivateCalls { get; } = [];

    public Task<ContractDetailsDto?> GetByIdAsync(Guid contractId, CancellationToken ct)
    {
        GetByIdCalls.Add((contractId, ct));
        return Task.FromResult(GetByIdResult);
    }

    public Task<GetContractHistoryResponse> GetHistoryAsync(
        GetContractHistoryRequest request,
        CancellationToken ct
    )
    {
        HistoryCalls.Add((request, ct));
        return Task.FromResult(HistoryResult);
    }

    public Task<ContractDetailsDto> CreateAtomicAsync(
        CreateContractRequest request,
        Guid actorId,
        CancellationToken ct
    )
    {
        CreateCalls.Add((request, actorId, ct));
        return Task.FromResult(CreateResult);
    }

    public Task<ContractDetailsDto?> UpdateAtomicAsync(
        UpdateContractRequest request,
        Guid actorId,
        CancellationToken ct
    )
    {
        UpdateCalls.Add((request, actorId, ct));
        return Task.FromResult(UpdateResult);
    }

    public Task<MutationOutcome?> DeactivateAtomicAsync(
        Guid contractId,
        long version,
        string reason,
        Guid actorId,
        CancellationToken ct
    )
    {
        DeactivateCalls.Add((contractId, version, reason, actorId, ct));
        return Task.FromResult(DeactivateResult);
    }
}
