using System.Text.Json;
using Dapper;
using FastEndpoints;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.DTOs;
using Tradebook.Infrastructure.Data;

namespace Tradebook.Api.Features.Activity;

public sealed class GetActivityEndpoint(INpgsqlConnectionFactory connections)
    : Endpoint<GetActivityRequest, GetActivityResponse>
{
    private static readonly HashSet<string> AllowedEntities = new(StringComparer.Ordinal)
    {
        "bioticket_deliveries",
        "capacity_bookings",
        "contracts",
        "goo_certificate_transactions",
        "hedges",
        "market_prices",
        "physical_deliveries",
        "tax_tariffs",
        "transfers",
    };

    public override void Configure()
    {
        Get("/api/v1/activity/{entityName}/{entityId}");
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(GetActivityRequest request, CancellationToken ct)
    {
        if (
            !AllowedEntities.Contains(request.EntityName)
            || string.IsNullOrWhiteSpace(request.EntityId)
            || request.EntityId.Length > 128
            || request.PageSize is < 1 or > 200
        )
        {
            AddError("The requested activity stream is invalid.");
            await Send.ErrorsAsync(cancellation: ct).ConfigureAwait(false);
            return;
        }

        var connection = await connections.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var configuredConnection = connection.ConfigureAwait(false);
        var rows = await connection
            .QueryAsync<ActivityRow>(
                new CommandDefinition(
                    """
                    SELECT audit_id AS "AuditId",
                           operation AS "Operation",
                           actor_id AS "ActorId",
                           lower(system_time) AS "OccurredAt",
                           diff_patch::text AS "ChangesJson"
                    FROM audit_log
                    WHERE entity_name = @EntityName AND entity_id = @EntityId
                    ORDER BY lower(system_time) DESC
                    LIMIT @PageSize
                    """,
                    new
                    {
                        request.EntityName,
                        request.EntityId,
                        request.PageSize,
                    },
                    cancellationToken: ct
                )
            )
            .ConfigureAwait(false);

        var items = rows.Select(row => new ActivityEntryDto(
                AuditLogId.From(row.AuditId),
                row.Operation,
                row.ActorId == Guid.Empty ? null : UserId.From(row.ActorId),
                new DateTimeOffset(DateTime.SpecifyKind(row.OccurredAt, DateTimeKind.Utc)),
                JsonSerializer.Deserialize<JsonElement>(row.ChangesJson)
            ))
            .ToArray();
        await Send.OkAsync(new GetActivityResponse(items), ct).ConfigureAwait(false);
    }

    private sealed record ActivityRow(
        Guid AuditId,
        string Operation,
        Guid ActorId,
        DateTime OccurredAt,
        string ChangesJson
    );
}
