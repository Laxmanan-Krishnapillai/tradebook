using System.Security.Claims;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

internal sealed class RecordingHedgeRepository : IHedgeRepository
{
    public HedgeDetailsDto CreateResult { get; set; } = HandlerGroupBTestData.Hedge();
    public HedgeDetailsDto? UpdateResult { get; set; }
    public HedgeDetailsDto? GetByIdResult { get; set; }
    public MutationOutcome? DeleteResult { get; set; }
    public GetHedgeHistoryResponse HistoryResult { get; set; } =
        new([HandlerGroupBTestData.Hedge()], 1, 1, 50, false);

    public List<(Guid Id, CancellationToken Token)> GetByIdCalls { get; } = [];
    public List<(GetHedgeHistoryRequest Request, CancellationToken Token)> HistoryCalls { get; } =
    [];
    public List<(
        CreateHedgeRequest Request,
        Guid ActorId,
        CancellationToken Token
    )> CreateCalls { get; } = [];
    public List<(
        UpdateHedgeRequest Request,
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

    public Task<HedgeDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        GetByIdCalls.Add((id, ct));
        return Task.FromResult(GetByIdResult);
    }

    public Task<GetHedgeHistoryResponse> GetHistoryAsync(
        GetHedgeHistoryRequest request,
        CancellationToken ct
    )
    {
        HistoryCalls.Add((request, ct));
        return Task.FromResult(HistoryResult);
    }

    public Task<HedgeDetailsDto> CreateAtomicAsync(
        CreateHedgeRequest request,
        Guid actorId,
        CancellationToken ct
    )
    {
        CreateCalls.Add((request, actorId, ct));
        return Task.FromResult(CreateResult);
    }

    public Task<HedgeDetailsDto?> UpdateAtomicAsync(
        UpdateHedgeRequest request,
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
