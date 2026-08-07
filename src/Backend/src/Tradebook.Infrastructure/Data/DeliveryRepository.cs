using System.Data;
using System.Text.Json;
using Dapper;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Infrastructure.Data;

public sealed class DeliveryRepository(INpgsqlConnectionFactory connections) : IDeliveryRepository
{
    private sealed record DeliveryRow(
        Guid DeliveryId, Guid ContractId, string ContractInstanceId, string BookType,
        DateTime SupplyMonth, decimal? CapacityMw, decimal? VolumeNominatedMwh,
        decimal? VolumeRealisedMwh, decimal? VolumeMwh, string? PriceMechanism,
        decimal? RevenueEur, decimal? SubtotalEur, decimal? VatEur,
        decimal? InvoiceAmountEur, string Status, long Version, DateTime CreatedAt,
        DateTime UpdatedAt);

    private const string DetailsProjection = """
        id AS DeliveryId, contract_id AS ContractId, contract_instance_id AS ContractInstanceId,
        book_type::text AS BookType, supply_month AS SupplyMonth, capacity_mw AS CapacityMw,
        volume_nominated_mwh AS VolumeNominatedMwh, volume_realised_mwh AS VolumeRealisedMwh,
        volume_mwh AS VolumeMwh, price_mechanism::text AS PriceMechanism, revenue_eur AS RevenueEur,
        subtotal_eur AS SubtotalEur, vat_eur AS VatEur, invoice_amount_eur AS InvoiceAmountEur,
        status::text AS Status, version AS Version, created_at AS CreatedAt, updated_at AS UpdatedAt
        """;

    public async Task<PhysicalDeliveryDetailsDto?> GetByIdAsync(Guid deliveryId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<DeliveryRow>(new CommandDefinition($"SELECT {DetailsProjection} FROM physical_deliveries WHERE id = @DeliveryId", new { DeliveryId = deliveryId }, cancellationToken: cancellationToken));
        return row is null ? null : ToDto(row);
    }

    public async Task<GetDeliveryHistoryResponse> GetHistoryAsync(GetDeliveryHistoryRequest request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var filters = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("Limit", pageSize);
        parameters.Add("Offset", (page - 1) * pageSize);
        if (request.ContractId is { } contractId) { filters.Add("contract_id = @ContractId"); parameters.Add("ContractId", contractId); }
        if (!string.IsNullOrWhiteSpace(request.ContractInstanceId)) { filters.Add("contract_instance_id = @ContractInstanceId"); parameters.Add("ContractInstanceId", request.ContractInstanceId); }
        if (!string.IsNullOrWhiteSpace(request.BookType)) { filters.Add("book_type::text = @BookType"); parameters.Add("BookType", request.BookType); }
        if (!string.IsNullOrWhiteSpace(request.Status)) { filters.Add("status::text = @Status"); parameters.Add("Status", request.Status); }
        if (request.FromMonth is { } fromMonth) { filters.Add("supply_month >= @FromMonth"); parameters.Add("FromMonth", fromMonth); }
        if (request.ToMonth is { } toMonth) { filters.Add("supply_month <= @ToMonth"); parameters.Add("ToMonth", toMonth); }
        var where = filters.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", filters);
        var rowsSql = $"SELECT {DetailsProjection} FROM physical_deliveries{where} ORDER BY supply_month DESC, contract_instance_id LIMIT @Limit OFFSET @Offset";
        var countSql = $"SELECT COUNT(*) FROM physical_deliveries{where}";
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        var items = (await connection.QueryAsync<DeliveryRow>(new CommandDefinition(rowsSql, parameters, cancellationToken: cancellationToken))).Select(ToDto).ToList();
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken));
        return new GetDeliveryHistoryResponse(items.AsReadOnly(), total, page, pageSize, page * pageSize < total);
    }

    public async Task<PhysicalDeliveryDetailsDto> CreateAtomicAsync(CreatePhysicalDeliveryRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        var deliveryId = Guid.NewGuid();
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await SetActorAsync(connection, transaction, actorId, cancellationToken);
        const string insert = """INSERT INTO physical_deliveries (id, contract_id, contract_instance_id, book_type, supply_month, capacity_mw, volume_nominated_mwh, volume_realised_mwh, volume_mwh, price_mechanism, start_day, end_day) VALUES (@DeliveryId, @ContractId, @ContractInstanceId, @BookType::book_type_enum, @SupplyMonth, @CapacityMw, @VolumeNominatedMwh, @VolumeRealisedMwh, @VolumeRealisedMwh, @PriceMechanism::gas_price_mech_enum, @StartDay, @EndDay) RETURNING """ + DetailsProjection;
        var details = ToDto(await connection.QuerySingleAsync<DeliveryRow>(new CommandDefinition(insert, new { DeliveryId = deliveryId, request.ContractId, request.ContractInstanceId, request.BookType, request.SupplyMonth, request.CapacityMw, request.VolumeNominatedMwh, request.VolumeRealisedMwh, request.PriceMechanism, request.StartDay, request.EndDay }, transaction, cancellationToken: cancellationToken)));
        await WriteOutboxAsync(connection, transaction, deliveryId, "Created", details, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return details;
    }

    public async Task<PhysicalDeliveryDetailsDto?> UpdateAtomicAsync(UpdatePhysicalDeliveryRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await SetActorAsync(connection, transaction, actorId, cancellationToken);
        var sql = """UPDATE physical_deliveries SET volume_realised_mwh = COALESCE(@VolumeRealisedMwh, volume_realised_mwh), volume_mwh = COALESCE(@VolumeRealisedMwh, volume_mwh), status = COALESCE(@Status::report_status_enum, status), updated_at = clock_timestamp(), version = version + 1 WHERE id = @DeliveryId AND version = @Version RETURNING """ + DetailsProjection;
        var row = await connection.QuerySingleOrDefaultAsync<DeliveryRow>(new CommandDefinition(sql, request, transaction, cancellationToken: cancellationToken));
        if (row is null) { await transaction.RollbackAsync(cancellationToken); return null; }
        var details = ToDto(row);
        await WriteOutboxAsync(connection, transaction, request.DeliveryId, "Updated", details, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return details;
    }

    public async Task<MutationOutcome?> CancelAtomicAsync(Guid deliveryId, long expectedVersion, string reason, Guid actorId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await SetActorAsync(connection, transaction, actorId, cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<DeliveryRow>(new CommandDefinition("UPDATE physical_deliveries SET status = 'Cancelled', updated_at = clock_timestamp(), version = version + 1 WHERE id = @DeliveryId AND version = @ExpectedVersion RETURNING " + DetailsProjection, new { DeliveryId = deliveryId, ExpectedVersion = expectedVersion }, transaction, cancellationToken: cancellationToken));
        if (row is not null) { await WriteOutboxAsync(connection, transaction, deliveryId, "Cancelled", new { Delivery = ToDto(row), Reason = reason }, cancellationToken); await transaction.CommitAsync(cancellationToken); return null; }
        await transaction.RollbackAsync(cancellationToken);
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM physical_deliveries WHERE id = @DeliveryId)", new { DeliveryId = deliveryId }, cancellationToken: cancellationToken));
        return exists ? MutationOutcome.VersionConflict : MutationOutcome.NotFound;
    }

    private static Task SetActorAsync(System.Data.IDbConnection connection, IDbTransaction transaction, Guid actorId, CancellationToken ct) => connection.ExecuteAsync(new CommandDefinition("SELECT set_config('app.actor_id', @ActorId, true)", new { ActorId = actorId.ToString() }, transaction, cancellationToken: ct));
    private static Task WriteOutboxAsync(System.Data.IDbConnection connection, IDbTransaction transaction, Guid deliveryId, string eventType, object payload, CancellationToken ct) => connection.ExecuteAsync(new CommandDefinition("INSERT INTO outbox_events (aggregate_type, aggregate_id, event_type, payload) VALUES ('PhysicalDelivery', @AggregateId, @EventType, @Payload::jsonb)", new { AggregateId = deliveryId.ToString(), EventType = eventType, Payload = JsonSerializer.Serialize(payload) }, transaction, cancellationToken: ct));
    private static PhysicalDeliveryDetailsDto ToDto(DeliveryRow row) => new(row.DeliveryId, row.ContractId, row.ContractInstanceId, row.BookType, DateOnly.FromDateTime(row.SupplyMonth), row.CapacityMw, row.VolumeNominatedMwh, row.VolumeRealisedMwh, row.VolumeMwh, row.PriceMechanism, row.RevenueEur, row.SubtotalEur, row.VatEur, row.InvoiceAmountEur, row.Status, row.Version, new DateTimeOffset(row.CreatedAt), new DateTimeOffset(row.UpdatedAt));
}
