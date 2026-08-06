using FastEndpoints;
using Npgsql;
using Tradebook.Infrastructure.Data;

namespace Tradebook.Api.Features.Events;

public sealed class GetEventsSinceRequest
{
    public long AfterSequence { get; init; }
    public int Limit { get; init; } = 500;
}

public sealed record OutboxEventDto(Guid EventId, long SequenceId, string AggregateType,
    Guid AggregateId, string EventType, string PayloadJson);

public sealed record GetEventsSinceResponse(IReadOnlyList<OutboxEventDto> Events, long LatestSequence);

public sealed class GetEventsSinceEndpoint(INpgsqlConnectionFactory connections)
    : Endpoint<GetEventsSinceRequest, GetEventsSinceResponse>
{
    public override void Configure()
    {
        Get("/api/v1/events");
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(GetEventsSinceRequest request, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit, 1, 500);
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT event_id, sequence_id, aggregate_type, aggregate_id::uuid, event_type, payload::text
            FROM outbox_events
            WHERE sequence_id > @afterSequence
            ORDER BY sequence_id ASC
            LIMIT @limit;
            SELECT COALESCE(MAX(sequence_id), 0) FROM outbox_events;
            """, connection);
        command.Parameters.AddWithValue("afterSequence", request.AfterSequence);
        command.Parameters.AddWithValue("limit", limit);

        var events = new List<OutboxEventDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new OutboxEventDto(reader.GetGuid(0), reader.GetInt64(1), reader.GetString(2),
                reader.GetGuid(3), reader.GetString(4), reader.GetString(5)));
        }

        await reader.NextResultAsync(cancellationToken);
        var latestSequence = await reader.ReadAsync(cancellationToken) ? reader.GetInt64(0) : 0;
        await SendAsync(new GetEventsSinceResponse(events, latestSequence), cancellation: cancellationToken);
    }
}
