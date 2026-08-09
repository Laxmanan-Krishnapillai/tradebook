using System.Security.Claims;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

internal sealed class FakeCapacityBookingEndpointRepository : ICapacityBookingRepository
{
    public CapacityBookingDetailsDto CreateResult { get; set; } =
        DomainEndpointTestData.CapacityBooking();
    public CapacityBookingDetailsDto? UpdateResult { get; set; }
    public CapacityBookingDetailsDto? GetByIdResult { get; set; }
    public MutationOutcome? DeleteResult { get; set; }
    public GetCapacityBookingHistoryResponse HistoryResult { get; set; } =
        new([DomainEndpointTestData.CapacityBooking()], 1, 1, 50, false);

    public List<(Guid Id, CancellationToken Token)> GetByIdCalls { get; } = [];
    public List<(
        GetCapacityBookingHistoryRequest Request,
        CancellationToken Token
    )> HistoryCalls { get; } = [];
    public List<(
        CreateCapacityBookingRequest Request,
        Guid ActorId,
        CancellationToken Token
    )> CreateCalls { get; } = [];
    public List<(
        UpdateCapacityBookingRequest Request,
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

    public Task<CapacityBookingDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        GetByIdCalls.Add((id, ct));
        return Task.FromResult(GetByIdResult);
    }

    public Task<GetCapacityBookingHistoryResponse> GetHistoryAsync(
        GetCapacityBookingHistoryRequest request,
        CancellationToken ct
    )
    {
        HistoryCalls.Add((request, ct));
        return Task.FromResult(HistoryResult);
    }

    public Task<CapacityBookingDetailsDto> CreateAtomicAsync(
        CreateCapacityBookingRequest request,
        Guid actorId,
        CancellationToken ct
    )
    {
        CreateCalls.Add((request, actorId, ct));
        return Task.FromResult(CreateResult);
    }

    public Task<CapacityBookingDetailsDto?> UpdateAtomicAsync(
        UpdateCapacityBookingRequest request,
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
