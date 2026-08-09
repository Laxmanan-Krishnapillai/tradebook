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
    ITransactionalEventPublisher publisher) : IGooCertificateRepository
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

    public async Task<GooCertificateTransactionDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<GooCertificateTransactionDetailsDto>(new CommandDefinition(
            $"SELECT {Projection} FROM goo_certificate_transactions WHERE id = @Id", new { Id = id }, cancellationToken: ct));
    }

    public async Task<GetGooCertificateHistoryResponse> GetHistoryAsync(GetGooCertificateHistoryRequest request, CancellationToken ct)
    {
        var (page, size, offset) = RepositoryMutation.Page(request.Page, request.PageSize);
        var filters = new List<string>();
        var parameters = new DynamicParameters(new { Limit = size, Offset = offset });
        if (request.ContractId is { } id) { filters.Add("(producer_contract_id = @ContractId OR customer_contract_id = @ContractId)"); parameters.Add("ContractId", id); }
        if (!string.IsNullOrWhiteSpace(request.Status)) { filters.Add("status::text = @Status"); parameters.Add("Status", request.Status); }
        if (request.FromDate is { } from) { filters.Add("transaction_start_date >= @FromDate"); parameters.Add("FromDate", from); }
        if (request.ToDate is { } to) { filters.Add("transaction_start_date <= @ToDate"); parameters.Add("ToDate", to); }
        var where = filters.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", filters);
        await using var connection = await connections.OpenConnectionAsync(ct);
        var items = (await connection.QueryAsync<GooCertificateTransactionDetailsDto>(new CommandDefinition(
            $"SELECT {Projection} FROM goo_certificate_transactions{where} ORDER BY transaction_start_date DESC NULLS LAST, id LIMIT @Limit OFFSET @Offset",
            parameters, cancellationToken: ct))).AsList();
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM goo_certificate_transactions{where}", parameters, cancellationToken: ct));
        return new(items.AsReadOnly(), total, page, size, offset + items.Count < total);
    }

    public async Task<GooCertificateTransactionDetailsDto> CreateAtomicAsync(CreateGooCertificateTransactionRequest request, Guid actorId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct);
        var created = await connection.QuerySingleAsync<GooCertificateTransactionDetailsDto>(new CommandDefinition("""
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
            """ + " " + Projection, request, transaction, cancellationToken: ct));
        await publisher.EnlistAsync((DbTransaction)transaction, ct);
        await publisher.PublishAsync(EntityChangedDomainEvent.Create(
            RealtimeAggregateTypes.GooCertificateTransaction, created.GooCertificateTransactionId.ToString(),
            "Created", created.Version));
        await transaction.CommitAsync(ct);
        await publisher.FlushAsync();
        return created;
    }

    public async Task<GooCertificateTransactionDetailsDto?> UpdateAtomicAsync(UpdateGooCertificateTransactionRequest request, Guid actorId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct);
        var updated = await connection.QuerySingleOrDefaultAsync<GooCertificateTransactionDetailsDto>(new CommandDefinition("""
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
            """ + " " + Projection, request, transaction, cancellationToken: ct));
        if (updated is null) { await transaction.RollbackAsync(ct); return null; }
        await publisher.EnlistAsync((DbTransaction)transaction, ct);
        await publisher.PublishAsync(EntityChangedDomainEvent.Create(
            RealtimeAggregateTypes.GooCertificateTransaction, updated.GooCertificateTransactionId.ToString(),
            "Updated", updated.Version));
        await transaction.CommitAsync(ct);
        await publisher.FlushAsync();
        return updated;
    }

    public async Task<GooCertificateTransactionDetailsDto?> RequestBatchExportAtomicAsync(Guid id, long version, Guid actorId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct);
        var updated = await connection.QuerySingleOrDefaultAsync<GooCertificateTransactionDetailsDto>(new CommandDefinition("""
            UPDATE goo_certificate_transactions
            SET status = 'Batch export requested', updated_at = clock_timestamp(), version = version + 1
            WHERE id = @Id AND version = @Version RETURNING
            """ + " " + Projection, new { Id = id, Version = version }, transaction, cancellationToken: ct));
        if (updated is null) { await transaction.RollbackAsync(ct); return null; }
        await publisher.EnlistAsync((DbTransaction)transaction, ct);
        await publisher.PublishAsync(EntityChangedDomainEvent.Create(
            RealtimeAggregateTypes.GooCertificateTransaction, id.ToString(),
            "BatchExportRequested", updated.Version));
        await transaction.CommitAsync(ct);
        await publisher.FlushAsync();
        return updated;
    }

    public async Task<MutationOutcome?> DeleteAtomicAsync(Guid id, long version, string reason, Guid actorId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct);
        var deletedVersion = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "DELETE FROM goo_certificate_transactions WHERE id = @Id AND version = @Version RETURNING version",
            new { Id = id, Version = version }, transaction, cancellationToken: ct));
        if (deletedVersion is not null)
        {
            await publisher.EnlistAsync((DbTransaction)transaction, ct);
            await publisher.PublishAsync(EntityChangedDomainEvent.Create(
                RealtimeAggregateTypes.GooCertificateTransaction, id.ToString(),
                "Deleted", deletedVersion.Value, reason));
            await transaction.CommitAsync(ct);
            await publisher.FlushAsync();
            return null;
        }
        await transaction.RollbackAsync(ct);
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM goo_certificate_transactions WHERE id = @Id)", new { Id = id }, cancellationToken: ct));
        return exists ? MutationOutcome.VersionConflict : MutationOutcome.NotFound;
    }
}
