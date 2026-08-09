using Tradebook.Core.DTOs;

namespace Tradebook.Core.Interfaces;

public interface IRealtimeEventReader
{
    Task<GetEventsSinceResponse> GetSinceAsync(
        long afterSequence,
        int limit,
        Guid actorId,
        CancellationToken cancellationToken);
}
