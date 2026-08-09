using System.Data;
using System.Data.Common;
using Dapper;
using Tradebook.Core.Domain;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;
using Tradebook.Core.Messaging;

namespace Tradebook.Infrastructure.Data;

public sealed class TaxTariffRepository(
    INpgsqlConnectionFactory connections,
    ITransactionalEventPublisher publisher
) : ITaxTariffRepository
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
        var connection = await connections.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await (
                connection.QuerySingleOrDefaultAsync<TaxTariffDetailsDto>(
                    new CommandDefinition(
                        $"SELECT {Projection} FROM tax_tariffs WHERE id = @Id",
                        new { Id = id },
                        cancellationToken: ct
                    )
                )
            ).ConfigureAwait(false);
        }
    }

    public async Task<GetTaxTariffHistoryResponse> GetHistoryAsync(
        GetTaxTariffHistoryRequest request,
        CancellationToken ct
    )
    {
        var (page, size, offset) = RepositoryMutation.Page(request.Page, request.PageSize);
        var parameters = new
        {
            Limit = size,
            Offset = offset,
            request.ContractId,
            request.EffectiveOn,
        };
        const string rowsSql =
            $"SELECT {Projection} FROM tax_tariffs WHERE (@ContractId IS NULL OR contract_id = @ContractId) AND (@EffectiveOn IS NULL OR (period_start <= @EffectiveOn AND period_end >= @EffectiveOn)) ORDER BY period_start DESC, id LIMIT @Limit OFFSET @Offset";
        const string countSql =
            "SELECT COUNT(*) FROM tax_tariffs WHERE (@ContractId IS NULL OR contract_id = @ContractId) AND (@EffectiveOn IS NULL OR (period_start <= @EffectiveOn AND period_end >= @EffectiveOn))";
        var connection = await connections.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var items = (
                await (
                    connection.QueryAsync<TaxTariffDetailsDto>(
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

    public async Task<TaxTariffDetailsDto> CreateAtomicAsync(
        CreateTaxTariffRequest request,
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
                    connection.QuerySingleAsync<TaxTariffDetailsDto>(
                        new CommandDefinition(
                            """
                            INSERT INTO tax_tariffs (
                                contract_id, counterparty_id, period_start, period_end, tax_local_cur_mwh,
                                tso_local_cur_mwh, dso_local_cur_mwh, dso_tariff_local_cur_day,
                                adm_fee_local_cur_mwh, bal_fee_local_cur_mwh, currency)
                            VALUES (
                                @ContractId, @CounterpartyId, @PeriodStart, @PeriodEnd, @TaxLocalCurMwh,
                                @TsoLocalCurMwh, @DsoLocalCurMwh, @DsoTariffLocalCurDay,
                                @AdmFeeLocalCurMwh, @BalFeeLocalCurMwh, @Currency)
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
                            RealtimeAggregateTypes.TaxTariff,
                            created.TaxTariffId.Value.ToString(),
                            "Created",
                            created.Version
                        )
                    )
                    .ConfigureAwait(false);
                await publisher.FlushAsync().ConfigureAwait(false);
                await (transaction.CommitAsync(ct)).ConfigureAwait(false);
                return created;
            }
        }
    }

    public async Task<TaxTariffDetailsDto?> UpdateAtomicAsync(
        UpdateTaxTariffRequest request,
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
                    connection.QuerySingleOrDefaultAsync<TaxTariffDetailsDto>(
                        new CommandDefinition(
                            """
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
                            RealtimeAggregateTypes.TaxTariff,
                            updated.TaxTariffId.Value.ToString(),
                            "Updated",
                            updated.Version
                        )
                    )
                    .ConfigureAwait(false);
                await publisher.FlushAsync().ConfigureAwait(false);
                await (transaction.CommitAsync(ct)).ConfigureAwait(false);
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
                            "DELETE FROM tax_tariffs WHERE id = @Id AND version = @Version RETURNING version",
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
                                RealtimeAggregateTypes.TaxTariff,
                                id.ToString(),
                                "Deleted",
                                deletedVersion.Value,
                                reason
                            )
                        )
                        .ConfigureAwait(false);
                    await publisher.FlushAsync().ConfigureAwait(false);
                    await (transaction.CommitAsync(ct)).ConfigureAwait(false);
                    return null;
                }
                await (transaction.RollbackAsync(ct)).ConfigureAwait(false);
                var exists = await (
                    connection.ExecuteScalarAsync<bool>(
                        new CommandDefinition(
                            "SELECT EXISTS(SELECT 1 FROM tax_tariffs WHERE id = @Id)",
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
