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
    ITransactionalEventPublisher publisher
) : ITransferRepository
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
        var connection = await connections.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await (
                connection.QuerySingleOrDefaultAsync<TransferDetailsDto>(
                    new CommandDefinition(
                        $"SELECT {Projection} FROM transfers WHERE id = @Id",
                        new { Id = id },
                        cancellationToken: ct
                    )
                )
            ).ConfigureAwait(false);
        }
    }

    public async Task<GetTransferHistoryResponse> GetHistoryAsync(
        GetTransferHistoryRequest request,
        CancellationToken ct
    )
    {
        var (page, size, offset) = RepositoryMutation.Page(request.Page, request.PageSize);
        var parameters = new
        {
            Limit = size,
            Offset = offset,
            request.ContractId,
            Status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status,
            request.FromMonth,
            request.ToMonth,
        };
        const string rowsSql =
            $"SELECT {Projection} FROM transfers WHERE (@ContractId::uuid IS NULL OR contract_id = @ContractId) AND (@Status::text IS NULL OR status::text = @Status) AND (@FromMonth::date IS NULL OR supply_month >= @FromMonth) AND (@ToMonth::date IS NULL OR supply_month <= @ToMonth) ORDER BY supply_month DESC, contract_instance_id LIMIT @Limit OFFSET @Offset";
        const string countSql =
            "SELECT COUNT(*) FROM transfers WHERE (@ContractId::uuid IS NULL OR contract_id = @ContractId) AND (@Status::text IS NULL OR status::text = @Status) AND (@FromMonth::date IS NULL OR supply_month >= @FromMonth) AND (@ToMonth::date IS NULL OR supply_month <= @ToMonth)";
        var connection = await connections.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var items = (
                await (
                    connection.QueryAsync<TransferDetailsDto>(
                        new CommandDefinition(rowsSql, parameters, cancellationToken: ct)
                    )
                ).ConfigureAwait(false)
            ).AsList();
            var total = await (
                connection.ExecuteScalarAsync<int>(
                    new CommandDefinition(countSql, parameters, cancellationToken: ct)
                )
            ).ConfigureAwait(false);
            return new(items.AsReadOnly(), total, page, size, offset + items.Count < total);
        }
    }

    public async Task<TransferDetailsDto> CreateAtomicAsync(
        CreateTransferRequest request,
        Guid actorId,
        CancellationToken ct
    )
    {
        var connection = await connections.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection
                .BeginTransactionAsync(IsolationLevel.ReadCommitted, ct)
                .ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                await (
                    RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct)
                ).ConfigureAwait(false);
                var created = await (
                    connection.QuerySingleAsync<TransferDetailsDto>(
                        new CommandDefinition(
                            """
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
                            """
                                + " "
                                + Projection,
                            request,
                            transaction,
                            cancellationToken: ct
                        )
                    )
                ).ConfigureAwait(false);
                await publisher.EnlistAsync((DbTransaction)transaction, ct).ConfigureAwait(false);
                await publisher
                    .PublishAsync(
                        EntityChangedDomainEvent.Create(
                            RealtimeAggregateTypes.Transfer,
                            created.TransferId.Value.ToString(),
                            "Created",
                            created.Version
                        )
                    )
                    .ConfigureAwait(false);
                await (transaction.CommitAsync(ct)).ConfigureAwait(false);
                await publisher.FlushAsync().ConfigureAwait(false);
                return created;
            }
        }
    }

    public async Task<TransferDetailsDto?> UpdateAtomicAsync(
        UpdateTransferRequest request,
        Guid actorId,
        CancellationToken ct
    )
    {
        var connection = await connections.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection
                .BeginTransactionAsync(IsolationLevel.ReadCommitted, ct)
                .ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                await (
                    RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct)
                ).ConfigureAwait(false);
                var updated = await (
                    connection.QuerySingleOrDefaultAsync<TransferDetailsDto>(
                        new CommandDefinition(
                            """
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
                            """
                                + " "
                                + Projection,
                            request,
                            transaction,
                            cancellationToken: ct
                        )
                    )
                ).ConfigureAwait(false);
                if (updated is null)
                {
                    await (transaction.RollbackAsync(ct)).ConfigureAwait(false);
                    return null;
                }
                await publisher.EnlistAsync((DbTransaction)transaction, ct).ConfigureAwait(false);
                await publisher
                    .PublishAsync(
                        EntityChangedDomainEvent.Create(
                            RealtimeAggregateTypes.Transfer,
                            updated.TransferId.Value.ToString(),
                            "Updated",
                            updated.Version
                        )
                    )
                    .ConfigureAwait(false);
                await (transaction.CommitAsync(ct)).ConfigureAwait(false);
                await publisher.FlushAsync().ConfigureAwait(false);
                return updated;
            }
        }
    }

    public async Task<MutationOutcome?> CancelAtomicAsync(
        Guid id,
        long version,
        string reason,
        Guid actorId,
        CancellationToken ct
    )
    {
        var connection = await connections.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection
                .BeginTransactionAsync(IsolationLevel.ReadCommitted, ct)
                .ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                await (
                    RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct)
                ).ConfigureAwait(false);
                var newVersion = await (
                    connection.ExecuteScalarAsync<long?>(
                        new CommandDefinition(
                            """
                            UPDATE transfers SET status = 'Cancelled', updated_at = clock_timestamp(), version = version + 1
                            WHERE id = @Id AND version = @Version RETURNING version
                            """,
                            new { Id = id, Version = version },
                            transaction,
                            cancellationToken: ct
                        )
                    )
                ).ConfigureAwait(false);
                if (newVersion is not null)
                {
                    await publisher
                        .EnlistAsync((DbTransaction)transaction, ct)
                        .ConfigureAwait(false);
                    await publisher
                        .PublishAsync(
                            EntityChangedDomainEvent.Create(
                                RealtimeAggregateTypes.Transfer,
                                id.ToString(),
                                "Cancelled",
                                newVersion.Value,
                                reason
                            )
                        )
                        .ConfigureAwait(false);
                    await (transaction.CommitAsync(ct)).ConfigureAwait(false);
                    await publisher.FlushAsync().ConfigureAwait(false);
                    return null;
                }
                await (transaction.RollbackAsync(ct)).ConfigureAwait(false);
                var exists = await (
                    connection.ExecuteScalarAsync<bool>(
                        new CommandDefinition(
                            "SELECT EXISTS(SELECT 1 FROM transfers WHERE id = @Id)",
                            new { Id = id },
                            cancellationToken: ct
                        )
                    )
                ).ConfigureAwait(false);
                return exists ? MutationOutcome.VersionConflict : MutationOutcome.NotFound;
            }
        }
    }
}
