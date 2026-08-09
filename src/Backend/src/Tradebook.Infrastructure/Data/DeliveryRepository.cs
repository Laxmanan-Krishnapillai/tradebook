using System.Data;
using Dapper;
using Tradebook.Core.Domain;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Infrastructure.Data;

public sealed class DeliveryRepository(INpgsqlConnectionFactory connections) : IDeliveryRepository
{
    private const string DetailsProjection = """
        id AS DeliveryId, contract_id AS ContractId, contract_instance_id AS ContractInstanceId,
        book_type::text AS BookType, supply_month AS SupplyMonth, capacity_mw AS CapacityMw,
        volume_nominated_mwh AS VolumeNominatedMwh, volume_realised_mwh AS VolumeRealisedMwh,
        volume_mwh AS VolumeMwh, price_mechanism::text AS PriceMechanism, revenue_eur AS RevenueEur,
        subtotal_eur AS SubtotalEur, vat_eur AS VatEur, invoice_amount_eur AS InvoiceAmountEur,
        status::text AS Status, version AS Version, created_at AS CreatedAt, updated_at AS UpdatedAt
        """;

    private const string InsertSql =
        """INSERT INTO physical_deliveries (id, contract_id, contract_instance_id, book_type, supply_month, capacity_mw, volume_nominated_mwh, volume_realised_mwh, volume_mwh, price_mechanism, start_day, end_day) VALUES (@DeliveryId, @ContractId, @ContractInstanceId, @BookType::book_type_enum, @SupplyMonth, @CapacityMw, @VolumeNominatedMwh, @VolumeRealisedMwh, @VolumeRealisedMwh, @PriceMechanism::gas_price_mech_enum, @StartDay, @EndDay) RETURNING """
        + DetailsProjection;

    public async Task<PhysicalDeliveryDetailsDto?> GetByIdAsync(
        Guid deliveryId,
        CancellationToken cancellationToken
    )
    {
        var connection = await connections
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var row = await (
                connection.QuerySingleOrDefaultAsync<DeliveryRow>(
                    new CommandDefinition(
                        $"SELECT {DetailsProjection} FROM physical_deliveries WHERE id = @DeliveryId",
                        new { DeliveryId = deliveryId },
                        cancellationToken: cancellationToken
                    )
                )
            ).ConfigureAwait(false);
            return row is null ? null : DeliveryMapper.ToDto(row);
        }
    }

    public async Task<GetDeliveryHistoryResponse> GetHistoryAsync(
        GetDeliveryHistoryRequest request,
        CancellationToken cancellationToken
    )
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var parameters = new
        {
            Limit = pageSize,
            Offset = (page - 1) * pageSize,
            request.ContractId,
            ContractInstanceId = string.IsNullOrWhiteSpace(request.ContractInstanceId)
                ? null
                : request.ContractInstanceId,
            BookType = string.IsNullOrWhiteSpace(request.BookType) ? null : request.BookType,
            Status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status,
            request.FromMonth,
            request.ToMonth,
        };
        const string rowsSql =
            $"SELECT {DetailsProjection} FROM physical_deliveries WHERE (@ContractId IS NULL OR contract_id = @ContractId) AND (@ContractInstanceId IS NULL OR contract_instance_id = @ContractInstanceId) AND (@BookType IS NULL OR book_type::text = @BookType) AND (@Status IS NULL OR status::text = @Status) AND (@FromMonth IS NULL OR supply_month >= @FromMonth) AND (@ToMonth IS NULL OR supply_month <= @ToMonth) ORDER BY supply_month DESC, contract_instance_id LIMIT @Limit OFFSET @Offset";
        const string countSql =
            "SELECT COUNT(*) FROM physical_deliveries WHERE (@ContractId IS NULL OR contract_id = @ContractId) AND (@ContractInstanceId IS NULL OR contract_instance_id = @ContractInstanceId) AND (@BookType IS NULL OR book_type::text = @BookType) AND (@Status IS NULL OR status::text = @Status) AND (@FromMonth IS NULL OR supply_month >= @FromMonth) AND (@ToMonth IS NULL OR supply_month <= @ToMonth)";
        var connection = await connections
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var items = (
                await (
                    connection.QueryAsync<DeliveryRow>(
                        new CommandDefinition(
                            rowsSql,
                            parameters,
                            cancellationToken: cancellationToken
                        )
                    )
                ).ConfigureAwait(false)
            )
                .Select(DeliveryMapper.ToDto)
                .ToList();
            var total = await (
                connection.ExecuteScalarAsync<int>(
                    new CommandDefinition(
                        countSql,
                        parameters,
                        cancellationToken: cancellationToken
                    )
                )
            ).ConfigureAwait(false);
            return new GetDeliveryHistoryResponse(
                items.AsReadOnly(),
                total,
                page,
                pageSize,
                page * pageSize < total
            );
        }
    }

    public async Task<PhysicalDeliveryDetailsDto> CreateAtomicAsync(
        CreatePhysicalDeliveryRequest request,
        Guid actorId,
        CancellationToken cancellationToken
    )
    {
        var deliveryId = Guid.NewGuid();
        var connection = await connections
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection
                .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
                .ConfigureAwait(false);
            await using var transactionLease = transaction.ConfigureAwait(false);
            await (
                RepositoryMutation.SetActorAsync(
                    connection,
                    transaction,
                    actorId,
                    cancellationToken
                )
            ).ConfigureAwait(false);
            var details = DeliveryMapper.ToDto(
                await (
                    connection.QuerySingleAsync<DeliveryRow>(
                        new CommandDefinition(
                            InsertSql,
                            new
                            {
                                DeliveryId = deliveryId,
                                request.ContractId,
                                request.ContractInstanceId,
                                request.BookType,
                                request.SupplyMonth,
                                request.CapacityMw,
                                request.VolumeNominatedMwh,
                                request.VolumeRealisedMwh,
                                request.PriceMechanism,
                                request.StartDay,
                                request.EndDay,
                            },
                            transaction,
                            cancellationToken: cancellationToken
                        )
                    )
                ).ConfigureAwait(false)
            );
            await (
                RepositoryMutation.WriteOutboxAsync(
                    connection,
                    transaction,
                    OutboxAggregateTypes.PhysicalDelivery,
                    deliveryId.ToString(),
                    "Created",
                    details.Version,
                    null,
                    cancellationToken
                )
            ).ConfigureAwait(false);
            await (transaction.CommitAsync(cancellationToken)).ConfigureAwait(false);
            return details;
        }
    }

    public async Task<PhysicalDeliveryDetailsDto?> UpdateAtomicAsync(
        UpdatePhysicalDeliveryRequest request,
        Guid actorId,
        CancellationToken cancellationToken
    )
    {
        var connection = await connections
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection
                .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
                .ConfigureAwait(false);
            await using var transactionLease = transaction.ConfigureAwait(false);
            await (
                RepositoryMutation.SetActorAsync(
                    connection,
                    transaction,
                    actorId,
                    cancellationToken
                )
            ).ConfigureAwait(false);
            var sql =
                """UPDATE physical_deliveries SET volume_realised_mwh = COALESCE(@VolumeRealisedMwh, volume_realised_mwh), volume_mwh = COALESCE(@VolumeRealisedMwh, volume_mwh), status = COALESCE(@Status::report_status_enum, status), updated_at = clock_timestamp(), version = version + 1 WHERE id = @DeliveryId AND version = @Version RETURNING """
                + DetailsProjection;
            var row = await (
                connection.QuerySingleOrDefaultAsync<DeliveryRow>(
                    new CommandDefinition(
                        sql,
                        request,
                        transaction,
                        cancellationToken: cancellationToken
                    )
                )
            ).ConfigureAwait(false);
            if (row is null)
            {
                await (transaction.RollbackAsync(cancellationToken)).ConfigureAwait(false);
                return null;
            }
            var details = DeliveryMapper.ToDto(row);
            await (
                RepositoryMutation.WriteOutboxAsync(
                    connection,
                    transaction,
                    OutboxAggregateTypes.PhysicalDelivery,
                    request.DeliveryId.ToString(),
                    "Updated",
                    details.Version,
                    null,
                    cancellationToken
                )
            ).ConfigureAwait(false);
            await (transaction.CommitAsync(cancellationToken)).ConfigureAwait(false);
            return details;
        }
    }

    public async Task<MutationOutcome?> CancelAtomicAsync(
        Guid deliveryId,
        long expectedVersion,
        string reason,
        Guid actorId,
        CancellationToken cancellationToken
    )
    {
        var connection = await connections
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection
                .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
                .ConfigureAwait(false);
            await using var transactionLease = transaction.ConfigureAwait(false);
            await (
                RepositoryMutation.SetActorAsync(
                    connection,
                    transaction,
                    actorId,
                    cancellationToken
                )
            ).ConfigureAwait(false);
            var row = await (
                connection.QuerySingleOrDefaultAsync<DeliveryRow>(
                    new CommandDefinition(
                        "UPDATE physical_deliveries SET status = 'Cancelled', updated_at = clock_timestamp(), version = version + 1 WHERE id = @DeliveryId AND version = @ExpectedVersion RETURNING "
                            + DetailsProjection,
                        new { DeliveryId = deliveryId, ExpectedVersion = expectedVersion },
                        transaction,
                        cancellationToken: cancellationToken
                    )
                )
            ).ConfigureAwait(false);
            if (row is not null)
            {
                await (
                    RepositoryMutation.WriteOutboxAsync(
                        connection,
                        transaction,
                        OutboxAggregateTypes.PhysicalDelivery,
                        deliveryId.ToString(),
                        "Cancelled",
                        row.Version,
                        reason,
                        cancellationToken
                    )
                ).ConfigureAwait(false);
                await (transaction.CommitAsync(cancellationToken)).ConfigureAwait(false);
                return null;
            }
            await (transaction.RollbackAsync(cancellationToken)).ConfigureAwait(false);
            var exists = await (
                connection.ExecuteScalarAsync<bool>(
                    new CommandDefinition(
                        "SELECT EXISTS(SELECT 1 FROM physical_deliveries WHERE id = @DeliveryId)",
                        new { DeliveryId = deliveryId },
                        cancellationToken: cancellationToken
                    )
                )
            ).ConfigureAwait(false);
            return exists ? MutationOutcome.VersionConflict : MutationOutcome.NotFound;
        }
    }
}
