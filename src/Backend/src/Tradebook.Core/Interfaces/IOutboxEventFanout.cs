namespace Tradebook.Core.Interfaces;

/// <summary>
/// Core-owned fan-out port for committed outbox events. Implemented by the API host
/// (SignalR hub context) and consumed by the Infrastructure outbox dispatcher, keeping
/// the Infrastructure -> Api dependency direction legal (see spec-issues 2026-08-06,
/// Task 03 dispatcher dependency direction).
/// </summary>
public interface IOutboxEventFanout
{
    Task PublishEntityChangedAsync(
        Guid eventId,
        long sequenceId,
        string aggregateType,
        string aggregateId,
        string eventType,
        string payloadJson,
        CancellationToken cancellationToken
    );
}
