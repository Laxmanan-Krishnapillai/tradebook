using System.Data;
using Dapper;
using Tradebook.Core.Domain;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Infrastructure.Data;

public sealed class HedgeRepository(INpgsqlConnectionFactory connections) : IHedgeRepository
{
    private const string Projection = """
        id AS HedgeId, contract_id AS ContractId, month AS Month,
        hedge_amount_mwh AS HedgeAmountMwh, hedge_price_eur_mwh AS HedgePriceEurMwh,
        version AS Version, created_at AS CreatedAt, updated_at AS UpdatedAt
        """;

    public async Task<HedgeDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var connection = await connections.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await (
                connection.QuerySingleOrDefaultAsync<HedgeDetailsDto>(
                    new CommandDefinition(
                        $"SELECT {Projection} FROM hedges WHERE id = @Id",
                        new { Id = id },
                        cancellationToken: ct
                    )
                )
            ).ConfigureAwait(false);
        }
    }

    public async Task<GetHedgeHistoryResponse> GetHistoryAsync(
        GetHedgeHistoryRequest request,
        CancellationToken ct
    )
    {
        var (page, size, offset) = RepositoryMutation.Page(request.Page, request.PageSize);
        var parameters = new
        {
            Limit = size,
            Offset = offset,
            request.ContractId,
            request.FromMonth,
            request.ToMonth,
        };
        const string rowsSql =
            $"SELECT {Projection} FROM hedges WHERE (@ContractId IS NULL OR contract_id = @ContractId) AND (@FromMonth IS NULL OR month >= @FromMonth) AND (@ToMonth IS NULL OR month <= @ToMonth) ORDER BY month DESC, id LIMIT @Limit OFFSET @Offset";
        const string countSql =
            "SELECT COUNT(*) FROM hedges WHERE (@ContractId IS NULL OR contract_id = @ContractId) AND (@FromMonth IS NULL OR month >= @FromMonth) AND (@ToMonth IS NULL OR month <= @ToMonth)";
        var connection = await connections.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var items = (
                await (
                    connection.QueryAsync<HedgeDetailsDto>(
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

    public async Task<HedgeDetailsDto> CreateAtomicAsync(
        CreateHedgeRequest request,
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
                    connection.QuerySingleAsync<HedgeDetailsDto>(
                        new CommandDefinition(
                            """
                            INSERT INTO hedges (contract_id, month, hedge_amount_mwh, hedge_price_eur_mwh)
                            VALUES (@ContractId, @Month, @HedgeAmountMwh, @HedgePriceEurMwh)
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
                await (
                    RepositoryMutation.WriteOutboxAsync(
                        connection,
                        transaction,
                        OutboxAggregateTypes.Hedge,
                        created.HedgeId.Value.ToString(),
                        "Created",
                        created.Version,
                        null,
                        ct
                    )
                ).ConfigureAwait(false);
                await (transaction.CommitAsync(ct)).ConfigureAwait(false);
                return created;
            }
        }
    }

    public async Task<HedgeDetailsDto?> UpdateAtomicAsync(
        UpdateHedgeRequest request,
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
                    connection.QuerySingleOrDefaultAsync<HedgeDetailsDto>(
                        new CommandDefinition(
                            """
                            UPDATE hedges SET
                                hedge_amount_mwh = COALESCE(@HedgeAmountMwh, hedge_amount_mwh),
                                hedge_price_eur_mwh = COALESCE(@HedgePriceEurMwh, hedge_price_eur_mwh),
                                updated_at = clock_timestamp(), version = version + 1
                            WHERE id = @HedgeId AND version = @Version
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
                await (
                    RepositoryMutation.WriteOutboxAsync(
                        connection,
                        transaction,
                        OutboxAggregateTypes.Hedge,
                        updated.HedgeId.Value.ToString(),
                        "Updated",
                        updated.Version,
                        null,
                        ct
                    )
                ).ConfigureAwait(false);
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
                            "DELETE FROM hedges WHERE id = @Id AND version = @Version RETURNING version",
                            new { Id = id, Version = version },
                            transaction,
                            cancellationToken: ct
                        )
                    )
                ).ConfigureAwait(false);
                if (deletedVersion is not null)
                {
                    await (
                        RepositoryMutation.WriteOutboxAsync(
                            connection,
                            transaction,
                            OutboxAggregateTypes.Hedge,
                            id.ToString(),
                            "Deleted",
                            deletedVersion.Value,
                            reason,
                            ct
                        )
                    ).ConfigureAwait(false);
                    await (transaction.CommitAsync(ct)).ConfigureAwait(false);
                    return null;
                }
                await (transaction.RollbackAsync(ct)).ConfigureAwait(false);
                var exists = await (
                    connection.ExecuteScalarAsync<bool>(
                        new CommandDefinition(
                            "SELECT EXISTS(SELECT 1 FROM hedges WHERE id = @Id)",
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
