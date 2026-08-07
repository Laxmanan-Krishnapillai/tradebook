using System.Data;
using Dapper;
using Tradebook.Core.Domain;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Infrastructure.Data;

public sealed class TaxTariffRepository(INpgsqlConnectionFactory connections) : ITaxTariffRepository
{
    private const string Projection = """
        id AS TaxTariffId, contract_id AS ContractId, counterparty_id AS CounterpartyId,
        period_start AS PeriodStart, period_end AS PeriodEnd, tax_local_cur_mwh AS TaxLocalCurMwh,
        tso_local_cur_mwh AS TsoLocalCurMwh, dso_local_cur_mwh AS DsoLocalCurMwh,
        dso_tariff_local_cur_day AS DsoTariffLocalCurDay,
        adm_fee_local_cur_mwh AS AdmFeeLocalCurMwh, bal_fee_local_cur_mwh AS BalFeeLocalCurMwh,
        currency AS Currency, version AS Version, created_at AS CreatedAt, updated_at AS UpdatedAt
        """;

    public async Task<TaxTariffDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<TaxTariffDetailsDto>(new CommandDefinition(
            $"SELECT {Projection} FROM tax_tariffs WHERE id = @Id", new { Id = id }, cancellationToken: ct));
    }

    public async Task<GetTaxTariffHistoryResponse> GetHistoryAsync(GetTaxTariffHistoryRequest request, CancellationToken ct)
    {
        var (page, size, offset) = RepositoryMutation.Page(request.Page, request.PageSize);
        var filters = new List<string>();
        var parameters = new DynamicParameters(new { Limit = size, Offset = offset });
        if (request.ContractId is { } id) { filters.Add("contract_id = @ContractId"); parameters.Add("ContractId", id); }
        if (request.EffectiveOn is { } date) { filters.Add("period_start <= @EffectiveOn AND period_end >= @EffectiveOn"); parameters.Add("EffectiveOn", date); }
        var where = filters.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", filters);
        await using var connection = await connections.OpenConnectionAsync(ct);
        var items = (await connection.QueryAsync<TaxTariffDetailsDto>(new CommandDefinition(
            $"SELECT {Projection} FROM tax_tariffs{where} ORDER BY period_start DESC, id LIMIT @Limit OFFSET @Offset",
            parameters, cancellationToken: ct))).AsList();
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM tax_tariffs{where}", parameters, cancellationToken: ct));
        return new(items.AsReadOnly(), total, page, size, offset + items.Count < total);
    }

    public async Task<TaxTariffDetailsDto> CreateAtomicAsync(CreateTaxTariffRequest request, Guid actorId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct);
        var created = await connection.QuerySingleAsync<TaxTariffDetailsDto>(new CommandDefinition("""
            INSERT INTO tax_tariffs (
                contract_id, counterparty_id, period_start, period_end, tax_local_cur_mwh,
                tso_local_cur_mwh, dso_local_cur_mwh, dso_tariff_local_cur_day,
                adm_fee_local_cur_mwh, bal_fee_local_cur_mwh, currency)
            VALUES (
                @ContractId, @CounterpartyId, @PeriodStart, @PeriodEnd, @TaxLocalCurMwh,
                @TsoLocalCurMwh, @DsoLocalCurMwh, @DsoTariffLocalCurDay,
                @AdmFeeLocalCurMwh, @BalFeeLocalCurMwh, @Currency)
            RETURNING
            """ + " " + Projection, request, transaction, cancellationToken: ct));
        await RepositoryMutation.WriteOutboxAsync(connection, transaction, OutboxAggregateTypes.TaxTariff,
            created.TaxTariffId.ToString(), "Created", created.Version, null, ct);
        await transaction.CommitAsync(ct);
        return created;
    }

    public async Task<TaxTariffDetailsDto?> UpdateAtomicAsync(UpdateTaxTariffRequest request, Guid actorId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct);
        var updated = await connection.QuerySingleOrDefaultAsync<TaxTariffDetailsDto>(new CommandDefinition("""
            UPDATE tax_tariffs SET
                tax_local_cur_mwh = COALESCE(@TaxLocalCurMwh, tax_local_cur_mwh),
                tso_local_cur_mwh = COALESCE(@TsoLocalCurMwh, tso_local_cur_mwh),
                dso_local_cur_mwh = COALESCE(@DsoLocalCurMwh, dso_local_cur_mwh),
                dso_tariff_local_cur_day = COALESCE(@DsoTariffLocalCurDay, dso_tariff_local_cur_day),
                adm_fee_local_cur_mwh = COALESCE(@AdmFeeLocalCurMwh, adm_fee_local_cur_mwh),
                bal_fee_local_cur_mwh = COALESCE(@BalFeeLocalCurMwh, bal_fee_local_cur_mwh),
                currency = @Currency, updated_at = clock_timestamp(), version = version + 1
            WHERE id = @TaxTariffId AND version = @Version
            RETURNING
            """ + " " + Projection, request, transaction, cancellationToken: ct));
        if (updated is null) { await transaction.RollbackAsync(ct); return null; }
        await RepositoryMutation.WriteOutboxAsync(connection, transaction, OutboxAggregateTypes.TaxTariff,
            updated.TaxTariffId.ToString(), "Updated", updated.Version, null, ct);
        await transaction.CommitAsync(ct);
        return updated;
    }

    public async Task<MutationOutcome?> DeleteAtomicAsync(Guid id, long version, string reason, Guid actorId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct);
        var deletedVersion = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "DELETE FROM tax_tariffs WHERE id = @Id AND version = @Version RETURNING version",
            new { Id = id, Version = version }, transaction, cancellationToken: ct));
        if (deletedVersion is not null)
        {
            await RepositoryMutation.WriteOutboxAsync(connection, transaction, OutboxAggregateTypes.TaxTariff,
                id.ToString(), "Deleted", deletedVersion.Value, reason, ct);
            await transaction.CommitAsync(ct);
            return null;
        }
        await transaction.RollbackAsync(ct);
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM tax_tariffs WHERE id = @Id)", new { Id = id }, cancellationToken: ct));
        return exists ? MutationOutcome.VersionConflict : MutationOutcome.NotFound;
    }
}
