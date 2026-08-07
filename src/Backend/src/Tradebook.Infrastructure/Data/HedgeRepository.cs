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
        await using var connection = await connections.OpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<HedgeDetailsDto>(new CommandDefinition(
            $"SELECT {Projection} FROM hedges WHERE id = @Id", new { Id = id }, cancellationToken: ct));
    }

    public async Task<GetHedgeHistoryResponse> GetHistoryAsync(GetHedgeHistoryRequest request, CancellationToken ct)
    {
        var (page, size, offset) = RepositoryMutation.Page(request.Page, request.PageSize);
        var filters = new List<string>();
        var parameters = new DynamicParameters(new { Limit = size, Offset = offset });
        if (request.ContractId is { } id) { filters.Add("contract_id = @ContractId"); parameters.Add("ContractId", id); }
        if (request.FromMonth is { } from) { filters.Add("month >= @FromMonth"); parameters.Add("FromMonth", from); }
        if (request.ToMonth is { } to) { filters.Add("month <= @ToMonth"); parameters.Add("ToMonth", to); }
        var where = filters.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", filters);
        await using var connection = await connections.OpenConnectionAsync(ct);
        var items = (await connection.QueryAsync<HedgeDetailsDto>(new CommandDefinition(
            $"SELECT {Projection} FROM hedges{where} ORDER BY month DESC, id LIMIT @Limit OFFSET @Offset",
            parameters, cancellationToken: ct))).AsList();
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM hedges{where}", parameters, cancellationToken: ct));
        return new(items.AsReadOnly(), total, page, size, offset + items.Count < total);
    }

    public async Task<HedgeDetailsDto> CreateAtomicAsync(CreateHedgeRequest request, Guid actorId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct);
        var created = await connection.QuerySingleAsync<HedgeDetailsDto>(new CommandDefinition("""
            INSERT INTO hedges (contract_id, month, hedge_amount_mwh, hedge_price_eur_mwh)
            VALUES (@ContractId, @Month, @HedgeAmountMwh, @HedgePriceEurMwh)
            RETURNING
            """ + " " + Projection, request, transaction, cancellationToken: ct));
        await RepositoryMutation.WriteOutboxAsync(connection, transaction, OutboxAggregateTypes.Hedge,
            created.HedgeId.ToString(), "Created", created.Version, null, ct);
        await transaction.CommitAsync(ct);
        return created;
    }

    public async Task<HedgeDetailsDto?> UpdateAtomicAsync(UpdateHedgeRequest request, Guid actorId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct);
        var updated = await connection.QuerySingleOrDefaultAsync<HedgeDetailsDto>(new CommandDefinition("""
            UPDATE hedges SET
                hedge_amount_mwh = COALESCE(@HedgeAmountMwh, hedge_amount_mwh),
                hedge_price_eur_mwh = COALESCE(@HedgePriceEurMwh, hedge_price_eur_mwh),
                updated_at = clock_timestamp(), version = version + 1
            WHERE id = @HedgeId AND version = @Version
            RETURNING
            """ + " " + Projection, request, transaction, cancellationToken: ct));
        if (updated is null) { await transaction.RollbackAsync(ct); return null; }
        await RepositoryMutation.WriteOutboxAsync(connection, transaction, OutboxAggregateTypes.Hedge,
            updated.HedgeId.ToString(), "Updated", updated.Version, null, ct);
        await transaction.CommitAsync(ct);
        return updated;
    }

    public async Task<MutationOutcome?> DeleteAtomicAsync(Guid id, long version, string reason, Guid actorId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct);
        var deletedVersion = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "DELETE FROM hedges WHERE id = @Id AND version = @Version RETURNING version",
            new { Id = id, Version = version }, transaction, cancellationToken: ct));
        if (deletedVersion is not null)
        {
            await RepositoryMutation.WriteOutboxAsync(connection, transaction, OutboxAggregateTypes.Hedge,
                id.ToString(), "Deleted", deletedVersion.Value, reason, ct);
            await transaction.CommitAsync(ct);
            return null;
        }
        await transaction.RollbackAsync(ct);
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM hedges WHERE id = @Id)", new { Id = id }, cancellationToken: ct));
        return exists ? MutationOutcome.VersionConflict : MutationOutcome.NotFound;
    }
}
