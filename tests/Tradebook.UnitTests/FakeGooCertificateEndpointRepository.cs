using System.Security.Claims;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

internal sealed class FakeGooCertificateEndpointRepository : IGooCertificateRepository
{
    public GooCertificateTransactionDetailsDto CreateResult { get; set; } =
        DomainEndpointTestData.GooCertificate();
    public GooCertificateTransactionDetailsDto? UpdateResult { get; set; }
    public GooCertificateTransactionDetailsDto? BatchExportResult { get; set; }
    public GooCertificateTransactionDetailsDto? GetByIdResult { get; set; }
    public MutationOutcome? DeleteResult { get; set; }
    public GetGooCertificateHistoryResponse HistoryResult { get; set; } =
        new([DomainEndpointTestData.GooCertificate()], 1, 1, 50, false);

    public List<(Guid Id, CancellationToken Token)> GetByIdCalls { get; } = [];
    public List<(
        GetGooCertificateHistoryRequest Request,
        CancellationToken Token
    )> HistoryCalls { get; } = [];
    public List<(
        CreateGooCertificateTransactionRequest Request,
        Guid ActorId,
        CancellationToken Token
    )> CreateCalls { get; } = [];
    public List<(
        UpdateGooCertificateTransactionRequest Request,
        Guid ActorId,
        CancellationToken Token
    )> UpdateCalls { get; } = [];
    public List<(
        Guid Id,
        long Version,
        Guid ActorId,
        CancellationToken Token
    )> BatchExportCalls { get; } = [];
    public List<(
        Guid Id,
        long Version,
        string Reason,
        Guid ActorId,
        CancellationToken Token
    )> DeleteCalls { get; } = [];

    public Task<GooCertificateTransactionDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        GetByIdCalls.Add((id, ct));
        return Task.FromResult(GetByIdResult);
    }

    public Task<GetGooCertificateHistoryResponse> GetHistoryAsync(
        GetGooCertificateHistoryRequest request,
        CancellationToken ct
    )
    {
        HistoryCalls.Add((request, ct));
        return Task.FromResult(HistoryResult);
    }

    public Task<GooCertificateTransactionDetailsDto> CreateAtomicAsync(
        CreateGooCertificateTransactionRequest request,
        Guid actorId,
        CancellationToken ct
    )
    {
        CreateCalls.Add((request, actorId, ct));
        return Task.FromResult(CreateResult);
    }

    public Task<GooCertificateTransactionDetailsDto?> UpdateAtomicAsync(
        UpdateGooCertificateTransactionRequest request,
        Guid actorId,
        CancellationToken ct
    )
    {
        UpdateCalls.Add((request, actorId, ct));
        return Task.FromResult(UpdateResult);
    }

    public Task<GooCertificateTransactionDetailsDto?> RequestBatchExportAtomicAsync(
        Guid id,
        long version,
        Guid actorId,
        CancellationToken ct
    )
    {
        BatchExportCalls.Add((id, version, actorId, ct));
        return Task.FromResult(BatchExportResult);
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
