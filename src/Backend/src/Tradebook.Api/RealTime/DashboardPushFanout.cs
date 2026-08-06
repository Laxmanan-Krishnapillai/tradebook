using Microsoft.AspNetCore.SignalR;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.RealTime;

/// <summary>
/// API-side implementation of the Core fan-out port: pushes committed outbox events to
/// the per-entity SignalR groups defined by <see cref="DashboardPushHub"/>.
/// </summary>
internal sealed class DashboardPushFanout(IHubContext<DashboardPushHub, IDashboardPushClient> hub) : IOutboxEventFanout
{
    public Task PublishEntityChangedAsync(Guid eventId, long sequenceId, string aggregateType,
        Guid aggregateId, string eventType, string payloadJson, CancellationToken cancellationToken)
        => hub.Clients.Group($"entity:{aggregateType}")
            .EntityChanged(eventId, sequenceId, aggregateType, aggregateId, eventType, payloadJson);
}
