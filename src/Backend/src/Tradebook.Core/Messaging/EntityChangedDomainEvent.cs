using System.Text.Json;
using System.Text.Json.Serialization;
using Tradebook.Core.Domain;

namespace Tradebook.Core.Messaging;

public sealed record EntityChangedDomainEvent(
    Guid EventId,
    string AggregateType,
    string AggregateId,
    string EventType,
    string PayloadJson)
{
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static EntityChangedDomainEvent Create(
        string aggregateType,
        string aggregateId,
        string eventType,
        long version,
        string? reason = null,
        Guid? actorId = null)
    {
        if (!RealtimeAggregateTypes.IsKnown(aggregateType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(aggregateType), aggregateType, "Unknown realtime aggregate type.");
        }

        var isWorkspaceDashboard = aggregateType == RealtimeAggregateTypes.WorkspaceDashboard;
        if (isWorkspaceDashboard != actorId.HasValue)
        {
            throw new ArgumentException(
                "actorId is required only for WorkspaceDashboard events.", nameof(actorId));
        }

        var payloadJson = isWorkspaceDashboard
            ? JsonSerializer.Serialize(
                new { dashboardId = aggregateId, actorId = actorId!.Value, version }, PayloadOptions)
            : JsonSerializer.Serialize(new { aggregateId, version, reason }, PayloadOptions);

        return new EntityChangedDomainEvent(
            Guid.NewGuid(), aggregateType, aggregateId, eventType, payloadJson);
    }
}
