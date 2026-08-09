using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Tradebook.Core.Domain;
using Tradebook.Core.Messaging;
using Tradebook.Infrastructure.Data;

namespace Tradebook.Api.RealTime.Handlers;

public sealed class EntityChangedRealtimeHandler(
    IHubContext<DashboardPushHub, IDashboardPushClient> hub,
    INpgsqlConnectionFactory connections)
{
    public async Task Handle(
        EntityChangedDomainEvent message,
        CancellationToken cancellationToken)
    {
        var group = message.AggregateType == RealtimeAggregateTypes.WorkspaceDashboard
            ? WorkspaceGroup(message.PayloadJson)
            : $"entity:{message.AggregateType}";

        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO realtime_event_log (
                event_id,
                group_name,
                aggregate_type,
                aggregate_id,
                event_type,
                payload)
            VALUES (
                @eventId,
                @groupName,
                @aggregateType,
                @aggregateId,
                @eventType,
                CAST(@payloadJson AS jsonb))
            ON CONFLICT (event_id) DO NOTHING
            RETURNING sequence_id;
            """;
        command.Parameters.AddWithValue("eventId", message.EventId);
        command.Parameters.AddWithValue("groupName", group);
        command.Parameters.AddWithValue("aggregateType", message.AggregateType);
        command.Parameters.AddWithValue("aggregateId", message.AggregateId);
        command.Parameters.AddWithValue("eventType", message.EventType);
        command.Parameters.AddWithValue("payloadJson", message.PayloadJson);

        var insertedSequence = await command.ExecuteScalarAsync(cancellationToken);
        if (insertedSequence is not long sequenceId)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        await hub.Clients.Group(group).EntityChanged(
            message.EventId,
            sequenceId,
            message.AggregateType,
            message.AggregateId,
            message.EventType,
            message.PayloadJson);
        await transaction.CommitAsync(cancellationToken);
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
            "WorkspaceDashboard domain event payload must contain a UUID actorId.",
            inner);
}
