using System.Security.Claims;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

internal sealed class RecordingTransferRepository : ITransferRepository
{
    public TransferDetailsDto CreateResult { get; set; } = HandlerGroupBTestData.Transfer();
    public TransferDetailsDto? UpdateResult { get; set; }
    public TransferDetailsDto? GetByIdResult { get; set; }
    public MutationOutcome? CancelResult { get; set; }
    public GetTransferHistoryResponse HistoryResult { get; set; } =
        new([HandlerGroupBTestData.Transfer()], 1, 1, 50, false);

    public List<(Guid Id, CancellationToken Token)> GetByIdCalls { get; } = [];
    public List<(
        GetTransferHistoryRequest Request,
        CancellationToken Token
    )> HistoryCalls { get; } = [];
    public List<(
        CreateTransferRequest Request,
        Guid ActorId,
        CancellationToken Token
    )> CreateCalls { get; } = [];
    public List<(
        UpdateTransferRequest Request,
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

    public Task<TransferDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        GetByIdCalls.Add((id, ct));
        return Task.FromResult(GetByIdResult);
    }

    public Task<GetTransferHistoryResponse> GetHistoryAsync(
        GetTransferHistoryRequest request,
        CancellationToken ct
    )
    {
        HistoryCalls.Add((request, ct));
        return Task.FromResult(HistoryResult);
    }

    public Task<TransferDetailsDto> CreateAtomicAsync(
        CreateTransferRequest request,
        Guid actorId,
        CancellationToken ct
    )
    {
        CreateCalls.Add((request, actorId, ct));
        return Task.FromResult(CreateResult);
    }

    public Task<TransferDetailsDto?> UpdateAtomicAsync(
        UpdateTransferRequest request,
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
