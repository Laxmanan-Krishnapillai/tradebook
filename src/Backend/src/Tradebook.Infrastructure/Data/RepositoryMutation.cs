using System.Data;
using Dapper;
using Tradebook.Core.Domain;

namespace Tradebook.Infrastructure.Data;

internal static class RepositoryMutation
{
    public static Task SetActorAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid actorId,
        CancellationToken ct
    ) =>
        connection.ExecuteAsync(
            new CommandDefinition(
                "SELECT set_config('app.actor_id', @ActorId, true)",
                new { ActorId = actorId.ToString() },
                transaction,
                cancellationToken: ct
            )
        );

    public static Task WriteOutboxAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        string aggregateType,
        string aggregateId,
        string eventType,
        long version,
        string? reason,
        CancellationToken ct
    )
    {
        if (!OutboxAggregateTypes.IsKnown(aggregateType))
            throw new ArgumentOutOfRangeException(
                nameof(aggregateType),
                aggregateType,
                "Unknown outbox aggregate type."
            );

        return connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO outbox_events (aggregate_type, aggregate_id, event_type, payload)
                VALUES (
                    @AggregateType,
                    @AggregateId,
                    @EventType,
                    jsonb_strip_nulls(jsonb_build_object(
                        'aggregateId', @AggregateId,
                        'version', @Version,
                        'reason', @Reason)))
                """,
                new
                {
                    AggregateType = aggregateType,
                    AggregateId = aggregateId,
                    EventType = eventType,
                    Version = version,
                    Reason = reason,
                },
                transaction,
                cancellationToken: ct
            )
        );
    }

    public static (int Page, int PageSize, int Offset) Page(
        int page,
        int pageSize,
        int maximum = 200
    )
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedSize = Math.Clamp(pageSize, 1, maximum);
        return (normalizedPage, normalizedSize, (normalizedPage - 1) * normalizedSize);
    }
}
