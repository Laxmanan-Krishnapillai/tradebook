using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Tradebook.Core.Interfaces;
using Tradebook.Core.Domain;

namespace Tradebook.Api.RealTime;

/// <summary>
/// API-side implementation of the Core fan-out port: pushes committed outbox events to
/// the per-entity SignalR groups defined by <see cref="DashboardPushHub"/>.
/// </summary>
internal sealed class DashboardPushFanout(IHubContext<DashboardPushHub, IDashboardPushClient> hub) : IOutboxEventFanout
{
    public Task PublishEntityChangedAsync(Guid eventId, long sequenceId, string aggregateType,
        string aggregateId, string eventType, string payloadJson, CancellationToken cancellationToken)
    {
        var group = aggregateType == OutboxAggregateTypes.WorkspaceDashboard
            ? WorkspaceGroup(payloadJson)
            : $"entity:{aggregateType}";
        return hub.Clients.Group(group).EntityChanged(
            eventId, sequenceId, aggregateType, aggregateId, eventType, payloadJson);
    }

    private static string WorkspaceGroup(string payloadJson)
    {
        try
        {
            using var payload = JsonDocument.Parse(payloadJson);
            return payload.RootElement.TryGetProperty("actorId", out var actor) &&
                   actor.ValueKind == JsonValueKind.String &&
                   Guid.TryParse(actor.GetString(), out var actorId)
                ? $"dashboard:{actorId}"
                : throw InvalidWorkspacePayload();
        }
        catch (JsonException exception)
        {
            throw InvalidWorkspacePayload(exception);
        }
    }

    private static InvalidOperationException InvalidWorkspacePayload(Exception? inner = null) =>
        new(
            "WorkspaceDashboard outbox payload must contain a UUID actorId.",
            inner);
}
