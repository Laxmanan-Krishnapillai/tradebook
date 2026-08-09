using System.Data;
using System.Data.Common;
using Dapper;
using Tradebook.Core.Domain;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;
using Tradebook.Core.Messaging;

namespace Tradebook.Infrastructure.Data;

public sealed class TransferRepository(
    INpgsqlConnectionFactory connections,
    ITransactionalEventPublisher publisher) : ITransferRepository
{
    private const string Projection = """
        id AS TransferId, contract_id AS ContractId, contract_instance_id AS ContractInstanceId,
        supply_month AS SupplyMonth, counterparty_id AS CounterpartyId,
        balancing_group AS BalancingGroup, trading_area AS TradingArea,
        capacity_mw AS CapacityMw, booked_capacity_mw AS BookedCapacityMw,
        volume_mwh AS VolumeMwh, balancing_effect_mwh AS BalancingEffectMwh,
        start_day AS StartDay, end_day AS EndDay, price_mechanism::text AS PriceMechanism,
        transport_cost_eur_mwh AS TransportCostEurMwh,
        capacity_cost_eur_mwh AS CapacityCostEurMwh, status::text AS Status,
        comments AS Comments, version AS Version, created_at AS CreatedAt, updated_at AS UpdatedAt
        """;

    public async Task<TransferDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<TransferDetailsDto>(new CommandDefinition(
            $"SELECT {Projection} FROM transfers WHERE id = @Id", new { Id = id }, cancellationToken: ct));
    }

    public async Task<GetTransferHistoryResponse> GetHistoryAsync(GetTransferHistoryRequest request, CancellationToken ct)
    {
        var (page, size, offset) = RepositoryMutation.Page(request.Page, request.PageSize);
        var filters = new List<string>();
        var parameters = new DynamicParameters(new { Limit = size, Offset = offset });
        if (request.ContractId is { } id) { filters.Add("contract_id = @ContractId"); parameters.Add("ContractId", id); }
        if (!string.IsNullOrWhiteSpace(request.Status)) { filters.Add("status::text = @Status"); parameters.Add("Status", request.Status); }
        if (request.FromMonth is { } from) { filters.Add("supply_month >= @FromMonth"); parameters.Add("FromMonth", from); }
        if (request.ToMonth is { } to) { filters.Add("supply_month <= @ToMonth"); parameters.Add("ToMonth", to); }
        var where = filters.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", filters);
        await using var connection = await connections.OpenConnectionAsync(ct);
        var items = (await connection.QueryAsync<TransferDetailsDto>(new CommandDefinition(
            $"SELECT {Projection} FROM transfers{where} ORDER BY supply_month DESC, contract_instance_id LIMIT @Limit OFFSET @Offset",
            parameters, cancellationToken: ct))).AsList();
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM transfers{where}", parameters, cancellationToken: ct));
        return new(items.AsReadOnly(), total, page, size, offset + items.Count < total);
    }

    public async Task<TransferDetailsDto> CreateAtomicAsync(CreateTransferRequest request, Guid actorId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct);
        var created = await connection.QuerySingleAsync<TransferDetailsDto>(new CommandDefinition("""
            INSERT INTO transfers (
                contract_id, contract_instance_id, supply_month, counterparty_id, balancing_group,
                trading_area, capacity_mw, booked_capacity_mw, volume_mwh, balancing_effect_mwh,
                start_day, end_day, price_mechanism, transport_cost_eur_mwh,
                capacity_cost_eur_mwh, status, comments)
            VALUES (
                @ContractId, @ContractInstanceId, @SupplyMonth, @CounterpartyId, @BalancingGroup,
                @TradingArea, @CapacityMw, @BookedCapacityMw, @VolumeMwh, @BalancingEffectMwh,
                @StartDay, @EndDay, CAST(@PriceMechanism AS gas_price_mech_enum),
                @TransportCostEurMwh, @CapacityCostEurMwh,
                CAST(@Status AS report_status_enum), @Comments)
            RETURNING
            """ + " " + Projection, request, transaction, cancellationToken: ct));
        await publisher.EnlistAsync((DbTransaction)transaction, ct);
        await publisher.PublishAsync(EntityChangedDomainEvent.Create(
            RealtimeAggregateTypes.Transfer, created.TransferId.ToString(), "Created", created.Version));
        await transaction.CommitAsync(ct);
        await publisher.FlushAsync();
        return created;
    }

    public async Task<TransferDetailsDto?> UpdateAtomicAsync(UpdateTransferRequest request, Guid actorId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct);
        var updated = await connection.QuerySingleOrDefaultAsync<TransferDetailsDto>(new CommandDefinition("""
            UPDATE transfers SET
                trading_area = COALESCE(@TradingArea, trading_area), capacity_mw = COALESCE(@CapacityMw, capacity_mw),
                booked_capacity_mw = COALESCE(@BookedCapacityMw, booked_capacity_mw),
                volume_mwh = COALESCE(@VolumeMwh, volume_mwh),
                balancing_effect_mwh = COALESCE(@BalancingEffectMwh, balancing_effect_mwh),
                price_mechanism = COALESCE(CAST(@PriceMechanism AS gas_price_mech_enum), price_mechanism),
                transport_cost_eur_mwh = COALESCE(@TransportCostEurMwh, transport_cost_eur_mwh),
                capacity_cost_eur_mwh = COALESCE(@CapacityCostEurMwh, capacity_cost_eur_mwh),
                status = COALESCE(CAST(@Status AS report_status_enum), status),
                comments = COALESCE(@Comments, comments), updated_at = clock_timestamp(), version = version + 1
            WHERE id = @TransferId AND version = @Version
            RETURNING
            """ + " " + Projection, request, transaction, cancellationToken: ct));
        if (updated is null) { await transaction.RollbackAsync(ct); return null; }
        await publisher.EnlistAsync((DbTransaction)transaction, ct);
        await publisher.PublishAsync(EntityChangedDomainEvent.Create(
            RealtimeAggregateTypes.Transfer, updated.TransferId.ToString(), "Updated", updated.Version));
        await transaction.CommitAsync(ct);
        await publisher.FlushAsync();
        return updated;
    }

    public async Task<MutationOutcome?> CancelAtomicAsync(Guid id, long version, string reason, Guid actorId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct);
        var newVersion = await connection.ExecuteScalarAsync<long?>(new CommandDefinition("""
            UPDATE transfers SET status = 'Cancelled', updated_at = clock_timestamp(), version = version + 1
            WHERE id = @Id AND version = @Version RETURNING version
            """, new { Id = id, Version = version }, transaction, cancellationToken: ct));
        if (newVersion is not null)
        {
            await publisher.EnlistAsync((DbTransaction)transaction, ct);
            await publisher.PublishAsync(EntityChangedDomainEvent.Create(
                RealtimeAggregateTypes.Transfer, id.ToString(), "Cancelled", newVersion.Value, reason));
            await transaction.CommitAsync(ct);
            await publisher.FlushAsync();
            return null;
        }
        await transaction.RollbackAsync(ct);
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM transfers WHERE id = @Id)", new { Id = id }, cancellationToken: ct));
        return exists ? MutationOutcome.VersionConflict : MutationOutcome.NotFound;
    }
}
