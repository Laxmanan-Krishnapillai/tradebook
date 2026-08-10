using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Tradebook.Core.Domain;
using Tradebook.Core.Messaging;
using Tradebook.Infrastructure.Data;

namespace Tradebook.Api.RealTime.Handlers;

public sealed class EntityChangedRealtimeHandler(
    IHubContext<DashboardPushHub, IDashboardPushClient> hub,
    INpgsqlConnectionFactory connections
)
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style",
        "VSTHRD200:Use Async suffix",
        Justification = "SignalR client contract / Wolverine handler naming convention."
    )]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "MA0051:Method is too long",
        Justification = "Single transactional dedupe-persist-push unit; decomposition post-merge."
    )]
    public async Task Handle(EntityChangedDomainEvent message, CancellationToken cancellationToken)
    {
        var group = string.Equals(
            message.AggregateType,
            RealtimeAggregateTypes.WorkspaceDashboard,
            StringComparison.Ordinal
        )
            ? WorkspaceGroup(message.PayloadJson)
            : $"entity:{message.AggregateType}";

        var connection = await connections
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var configuredConnection = connection.ConfigureAwait(false);
        var transaction = await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var configuredTransaction = transaction.ConfigureAwait(false);
        var command = connection.CreateCommand();
        await using var configuredCommand = command.ConfigureAwait(false);
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

        var insertedSequence = await command
            .ExecuteScalarAsync(cancellationToken)
            .ConfigureAwait(false);
        if (insertedSequence is not long sequenceId)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await hub
            .Clients.Group(group)
            .EntityChanged(
                message.EventId,
                sequenceId,
                message.AggregateType,
                message.AggregateId,
                message.EventType,
                message.PayloadJson
            )
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string WorkspaceGroup(string payloadJson)
    {
        try
        {
            using var payload = JsonDocument.Parse(payloadJson);
            return
                payload.RootElement.TryGetProperty("actorId", out var actor)
                && actor.ValueKind == JsonValueKind.String
                && Guid.TryParse(actor.GetString(), out var actorId)
                ? $"dashboard:{actorId}"
                : throw InvalidWorkspacePayload();
        }
        catch (JsonException exception)
        {
            throw InvalidWorkspacePayload(exception);
        }
    }

    private static InvalidOperationException InvalidWorkspacePayload(Exception? inner = null) =>
        new("WorkspaceDashboard domain event payload must contain a UUID actorId.", inner);
}
