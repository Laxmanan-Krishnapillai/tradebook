using System.Security.Claims;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

internal sealed class FakeBioticketEndpointRepository : IBioticketRepository
{
    public BioticketDetailsDto CreateResult { get; set; } = DomainEndpointTestData.Bioticket();
    public BioticketDetailsDto? UpdateResult { get; set; }
    public BioticketDetailsDto? GetByIdResult { get; set; }
    public MutationOutcome? CancelResult { get; set; }
    public GetBioticketHistoryResponse HistoryResult { get; set; } =
        new([DomainEndpointTestData.Bioticket()], 1, 1, 50, false);

    public List<(Guid Id, CancellationToken Token)> GetByIdCalls { get; } = [];
    public List<(
        GetBioticketHistoryRequest Request,
        CancellationToken Token
    )> HistoryCalls { get; } = [];
    public List<(
        CreateBioticketRequest Request,
        Guid ActorId,
        CancellationToken Token
    )> CreateCalls { get; } = [];
    public List<(
        UpdateBioticketRequest Request,
        Guid ActorId,
        CancellationToken Token
    )> UpdateCalls { get; } = [];
    public List<(
        Guid Id,
        long Version,
        string Reason,
        Guid ActorId,
        CancellationToken Token
    )> CancelCalls { get; } = [];

    public Task<BioticketDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        GetByIdCalls.Add((id, ct));
        return Task.FromResult(GetByIdResult);
    }

    public Task<GetBioticketHistoryResponse> GetHistoryAsync(
        GetBioticketHistoryRequest request,
        CancellationToken ct
    )
    {
        HistoryCalls.Add((request, ct));
        return Task.FromResult(HistoryResult);
    }

    public Task<BioticketDetailsDto> CreateAtomicAsync(
        CreateBioticketRequest request,
        Guid actorId,
        CancellationToken ct
    )
    {
        CreateCalls.Add((request, actorId, ct));
        return Task.FromResult(CreateResult);
    }

    public Task<BioticketDetailsDto?> UpdateAtomicAsync(
        UpdateBioticketRequest request,
        Guid actorId,
        CancellationToken ct
    )
    {
        UpdateCalls.Add((request, actorId, ct));
        return Task.FromResult(UpdateResult);
    }

    public Task<MutationOutcome?> CancelAtomicAsync(
        Guid id,
        long version,
        string reason,
        Guid actorId,
        CancellationToken ct
    )
    {
        CancelCalls.Add((id, version, reason, actorId, ct));
        return Task.FromResult(CancelResult);
    }
}
