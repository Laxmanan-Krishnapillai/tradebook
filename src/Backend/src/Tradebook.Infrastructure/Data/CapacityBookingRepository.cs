using System.Data;
using System.Data.Common;
using Dapper;
using Tradebook.Core.Domain;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;
using Tradebook.Core.Messaging;

namespace Tradebook.Infrastructure.Data;

public sealed class CapacityBookingRepository(
    INpgsqlConnectionFactory connections,
    ITransactionalEventPublisher publisher) : ICapacityBookingRepository
{
    private const string Projection = """
        id AS CapacityBookingId, contract_id AS ContractId,
        contract_instance_id AS ContractInstanceId, supply_month AS SupplyMonth,
        counterparty_id AS CounterpartyId, balancing_group AS BalancingGroup,
        price_mechanism::text AS PriceMechanism, start_area AS StartArea, end_area AS EndArea,
        ship_fix AS ShipFix, border_point AS BorderPoint, start_day AS StartDay, end_day AS EndDay,
        capacity_mw AS CapacityMw, capacity_price_eur_mwh AS CapacityPriceEurMwh,
        capacity_cost_eur AS CapacityCostEur, comments AS Comments,
        version AS Version, created_at AS CreatedAt, updated_at AS UpdatedAt
        """;

    public async Task<CapacityBookingDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<CapacityBookingDetailsDto>(new CommandDefinition(
            $"SELECT {Projection} FROM capacity_bookings WHERE id = @Id", new { Id = id }, cancellationToken: ct));
    }

    public async Task<GetCapacityBookingHistoryResponse> GetHistoryAsync(GetCapacityBookingHistoryRequest request, CancellationToken ct)
    {
        var (page, size, offset) = RepositoryMutation.Page(request.Page, request.PageSize);
        var filters = new List<string>();
        var parameters = new DynamicParameters(new { Limit = size, Offset = offset });
        if (request.ContractId is { } id) { filters.Add("contract_id = @ContractId"); parameters.Add("ContractId", id); }
        if (request.FromMonth is { } from) { filters.Add("supply_month >= @FromMonth"); parameters.Add("FromMonth", from); }
        if (request.ToMonth is { } to) { filters.Add("supply_month <= @ToMonth"); parameters.Add("ToMonth", to); }
        var where = filters.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", filters);
        await using var connection = await connections.OpenConnectionAsync(ct);
        var items = (await connection.QueryAsync<CapacityBookingDetailsDto>(new CommandDefinition(
            $"SELECT {Projection} FROM capacity_bookings{where} ORDER BY supply_month DESC, contract_instance_id LIMIT @Limit OFFSET @Offset",
            parameters, cancellationToken: ct))).AsList();
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM capacity_bookings{where}", parameters, cancellationToken: ct));
        return new(items.AsReadOnly(), total, page, size, offset + items.Count < total);
    }

    public async Task<CapacityBookingDetailsDto> CreateAtomicAsync(CreateCapacityBookingRequest request, Guid actorId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct);
        var created = await connection.QuerySingleAsync<CapacityBookingDetailsDto>(new CommandDefinition("""
            INSERT INTO capacity_bookings (
                contract_id, contract_instance_id, supply_month, counterparty_id, balancing_group,
                price_mechanism, start_area, end_area, ship_fix, border_point, start_day, end_day,
                capacity_mw, capacity_price_eur_mwh, capacity_cost_eur, comments)
            VALUES (
                @ContractId, @ContractInstanceId, @SupplyMonth, @CounterpartyId, @BalancingGroup,
                CAST(@PriceMechanism AS capacity_price_mech_enum), @StartArea, @EndArea, @ShipFix,
                @BorderPoint, @StartDay, @EndDay, @CapacityMw, @CapacityPriceEurMwh,
                @CapacityCostEur, @Comments)
            RETURNING
            """ + " " + Projection, request, transaction, cancellationToken: ct));
        await publisher.EnlistAsync((DbTransaction)transaction, ct);
        await publisher.PublishAsync(EntityChangedDomainEvent.Create(
            RealtimeAggregateTypes.CapacityBooking, created.CapacityBookingId.ToString(), "Created", created.Version));
        await transaction.CommitAsync(ct);
        await publisher.FlushAsync();
        return created;
    }

    public async Task<CapacityBookingDetailsDto?> UpdateAtomicAsync(UpdateCapacityBookingRequest request, Guid actorId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct);
        var updated = await connection.QuerySingleOrDefaultAsync<CapacityBookingDetailsDto>(new CommandDefinition("""
            UPDATE capacity_bookings SET
                balancing_group = COALESCE(@BalancingGroup, balancing_group),
                price_mechanism = COALESCE(CAST(@PriceMechanism AS capacity_price_mech_enum), price_mechanism),
                start_area = COALESCE(@StartArea, start_area), end_area = COALESCE(@EndArea, end_area),
                start_day = COALESCE(@StartDay, start_day), end_day = COALESCE(@EndDay, end_day),
                capacity_mw = COALESCE(@CapacityMw, capacity_mw),
                capacity_price_eur_mwh = COALESCE(@CapacityPriceEurMwh, capacity_price_eur_mwh),
                capacity_cost_eur = COALESCE(@CapacityCostEur, capacity_cost_eur),
                comments = COALESCE(@Comments, comments), updated_at = clock_timestamp(), version = version + 1
            WHERE id = @CapacityBookingId AND version = @Version
            RETURNING
            """ + " " + Projection, request, transaction, cancellationToken: ct));
        if (updated is null) { await transaction.RollbackAsync(ct); return null; }
        await publisher.EnlistAsync((DbTransaction)transaction, ct);
        await publisher.PublishAsync(EntityChangedDomainEvent.Create(
            RealtimeAggregateTypes.CapacityBooking, updated.CapacityBookingId.ToString(), "Updated", updated.Version));
        await transaction.CommitAsync(ct);
        await publisher.FlushAsync();
        return updated;
    }

    public Task<MutationOutcome?> DeleteAtomicAsync(Guid id, long version, string reason, Guid actorId, CancellationToken ct) =>
        DeleteInternalAsync(id, version, reason, actorId, ct);

    private async Task<MutationOutcome?> DeleteInternalAsync(Guid id, long version, string reason, Guid actorId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct);
        var deletedVersion = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "DELETE FROM capacity_bookings WHERE id = @Id AND version = @Version RETURNING version",
            new { Id = id, Version = version }, transaction, cancellationToken: ct));
        if (deletedVersion is not null)
        {
            await publisher.EnlistAsync((DbTransaction)transaction, ct);
            await publisher.PublishAsync(EntityChangedDomainEvent.Create(
                RealtimeAggregateTypes.CapacityBooking, id.ToString(), "Deleted", deletedVersion.Value, reason));
            await transaction.CommitAsync(ct);
            await publisher.FlushAsync();
            return null;
        }
        await transaction.RollbackAsync(ct);
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM capacity_bookings WHERE id = @Id)", new { Id = id }, cancellationToken: ct));
        return exists ? MutationOutcome.VersionConflict : MutationOutcome.NotFound;
    }
}
