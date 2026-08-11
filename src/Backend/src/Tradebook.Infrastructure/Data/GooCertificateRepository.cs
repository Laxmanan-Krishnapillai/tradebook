using System.Data;
using System.Data.Common;
using Dapper;
using Tradebook.Core.Domain;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;
using Tradebook.Core.Messaging;

namespace Tradebook.Infrastructure.Data;

public sealed class GooCertificateRepository(
    INpgsqlConnectionFactory connections,
    ITransactionalEventPublisher publisher
) : IGooCertificateRepository
{
    private const string Projection = """
        id AS GooCertificateTransactionId, sf_transaction_id AS SalesforceTransactionId,
        transaction_name AS TransactionName, batch_type AS BatchType,
        certificate_transaction_id AS CertificateTransactionId,
        country_of_production AS CountryOfProduction, producer_contract_id AS ProducerContractId,
        producer_company AS ProducerCompany, producer_goo_price_eur_mwh AS ProducerGooPriceEurMwh,
        production_date AS ProductionDate, customer_contract_id AS CustomerContractId,
        customer_company AS CustomerCompany, register AS Register, status::text AS Status,
        transaction_start_date AS TransactionStartDate,
        transaction_volume_mwh AS TransactionVolumeMwh, volume_mwh AS VolumeMwh,
        energy_source AS EnergySource, text AS Text, version AS Version,
        created_at AS CreatedAt, updated_at AS UpdatedAt
        """;

    public async Task<GooCertificateTransactionDetailsDto?> GetByIdAsync(
        Guid id,
        CancellationToken ct
    )
    {
        var connection = await connections.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await (
                connection.QuerySingleOrDefaultAsync<GooCertificateTransactionDetailsDto>(
                    new CommandDefinition(
                        $"SELECT {Projection} FROM goo_certificate_transactions WHERE id = @Id",
                        new { Id = id },
                        cancellationToken: ct
                    )
                )
            ).ConfigureAwait(false);
        }
    }

    public async Task<GetGooCertificateHistoryResponse> GetHistoryAsync(
        GetGooCertificateHistoryRequest request,
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
            request.FromDate,
            request.ToDate,
        };
        const string rowsSql =
            $"SELECT {Projection} FROM goo_certificate_transactions WHERE (@ContractId::uuid IS NULL OR producer_contract_id = @ContractId OR customer_contract_id = @ContractId) AND (@Status::text IS NULL OR status::text = @Status) AND (@FromDate::date IS NULL OR transaction_start_date >= @FromDate) AND (@ToDate::date IS NULL OR transaction_start_date <= @ToDate) ORDER BY transaction_start_date DESC NULLS LAST, id LIMIT @Limit OFFSET @Offset";
        const string countSql =
            "SELECT COUNT(*) FROM goo_certificate_transactions WHERE (@ContractId::uuid IS NULL OR producer_contract_id = @ContractId OR customer_contract_id = @ContractId) AND (@Status::text IS NULL OR status::text = @Status) AND (@FromDate::date IS NULL OR transaction_start_date >= @FromDate) AND (@ToDate::date IS NULL OR transaction_start_date <= @ToDate)";
        var connection = await connections.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var items = (
                await (
                    connection.QueryAsync<GooCertificateTransactionDetailsDto>(
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

    public async Task<GooCertificateTransactionDetailsDto> CreateAtomicAsync(
        CreateGooCertificateTransactionRequest request,
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
                    connection.QuerySingleAsync<GooCertificateTransactionDetailsDto>(
                        new CommandDefinition(
                            """
                            INSERT INTO goo_certificate_transactions (
                                sf_transaction_id, transaction_name, batch_type, certificate_transaction_id,
                                country_of_production, producer_contract_id, producer_company,
                                producer_goo_price_eur_mwh, production_date, customer_contract_id,
                                customer_company, register, status, transaction_start_date,
                                transaction_volume_mwh, volume_mwh, energy_source, text)
                            VALUES (
                                @SalesforceTransactionId, @TransactionName, @BatchType, @CertificateTransactionId,
                                @CountryOfProduction, @ProducerContractId, @ProducerCompany,
                                @ProducerGooPriceEurMwh, @ProductionDate, @CustomerContractId,
                                @CustomerCompany, @Register, CAST(@Status AS transaction_status_enum),
                                @TransactionStartDate, @TransactionVolumeMwh, @VolumeMwh, @EnergySource, @Text)
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
                            RealtimeAggregateTypes.GooCertificateTransaction,
                            created.GooCertificateTransactionId.Value.ToString(),
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

    public async Task<GooCertificateTransactionDetailsDto?> UpdateAtomicAsync(
        UpdateGooCertificateTransactionRequest request,
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
                    connection.QuerySingleOrDefaultAsync<GooCertificateTransactionDetailsDto>(
                        new CommandDefinition(
                            """
                            UPDATE goo_certificate_transactions SET
                                batch_type = COALESCE(@BatchType, batch_type),
                                producer_contract_id = COALESCE(@ProducerContractId, producer_contract_id),
                                customer_contract_id = COALESCE(@CustomerContractId, customer_contract_id),
                                register = COALESCE(@Register, register),
                                status = COALESCE(CAST(@Status AS transaction_status_enum), status),
                                transaction_start_date = COALESCE(@TransactionStartDate, transaction_start_date),
                                transaction_volume_mwh = COALESCE(@TransactionVolumeMwh, transaction_volume_mwh),
                                volume_mwh = COALESCE(@VolumeMwh, volume_mwh), text = COALESCE(@Text, text),
                                updated_at = clock_timestamp(), version = version + 1
                            WHERE id = @GooCertificateTransactionId AND version = @Version
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
                            RealtimeAggregateTypes.GooCertificateTransaction,
                            updated.GooCertificateTransactionId.Value.ToString(),
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

    public async Task<GooCertificateTransactionDetailsDto?> RequestBatchExportAtomicAsync(
        Guid id,
        long version,
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
                    connection.QuerySingleOrDefaultAsync<GooCertificateTransactionDetailsDto>(
                        new CommandDefinition(
                            """
                            UPDATE goo_certificate_transactions
                            SET status = 'Batch export requested', updated_at = clock_timestamp(), version = version + 1
                            WHERE id = @Id AND version = @Version RETURNING
                            """
                                + " "
                                + Projection,
                            new { Id = id, Version = version },
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
                            RealtimeAggregateTypes.GooCertificateTransaction,
                            id.ToString(),
                            "BatchExportRequested",
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

    public async Task<MutationOutcome?> DeleteAtomicAsync(
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
                var deletedVersion = await (
                    connection.ExecuteScalarAsync<long?>(
                        new CommandDefinition(
                            "DELETE FROM goo_certificate_transactions WHERE id = @Id AND version = @Version RETURNING version",
                            new { Id = id, Version = version },
                            transaction,
                            cancellationToken: ct
                        )
                    )
                ).ConfigureAwait(false);
                if (deletedVersion is not null)
                {
                    await publisher
                        .EnlistAsync((DbTransaction)transaction, ct)
                        .ConfigureAwait(false);
                    await publisher
                        .PublishAsync(
                            EntityChangedDomainEvent.Create(
                                RealtimeAggregateTypes.GooCertificateTransaction,
                                id.ToString(),
                                "Deleted",
                                deletedVersion.Value,
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
                            "SELECT EXISTS(SELECT 1 FROM goo_certificate_transactions WHERE id = @Id)",
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
