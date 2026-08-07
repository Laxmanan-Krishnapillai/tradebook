using System.Data;
using Dapper;
using Tradebook.Core.Domain;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Infrastructure.Data;

public sealed class MarketPriceRepository(INpgsqlConnectionFactory connections) : IMarketPriceRepository
{
    private const string Projection = """
        price_date AS PriceDate, ttf_eur_mwh AS TtfEurMwh,
        egsi_etf_eur_mwh AS EgsiEtfEurMwh, the_eur_mwh AS TheEurMwh,
        bgo_eur_mwh AS BgoEurMwh, pgo_eur_mwh AS PgoEurMwh, eua_eur_mwh AS EuaEurMwh,
        within_day_mkt_eur_mwh AS WithinDayMktEurMwh, eur_sek AS EurSek,
        eur_chf AS EurChf, eur_gbp AS EurGbp, eur_usd AS EurUsd, eur_dkk AS EurDkk,
        version AS Version, created_at AS CreatedAt
        """;

    public async Task<MarketPriceDetailsDto?> GetByDateAsync(DateOnly priceDate, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<MarketPriceDetailsDto>(new CommandDefinition(
            $"SELECT {Projection} FROM market_prices WHERE price_date = @PriceDate",
            new { PriceDate = priceDate }, cancellationToken: ct));
    }

    public async Task<GetMarketPriceHistoryResponse> GetHistoryAsync(GetMarketPriceHistoryRequest request, CancellationToken ct)
    {
        var (page, size, offset) = RepositoryMutation.Page(request.Page, request.PageSize, 500);
        var filters = new List<string>();
        var parameters = new DynamicParameters(new { Limit = size, Offset = offset });
        if (request.FromDate is { } from) { filters.Add("price_date >= @FromDate"); parameters.Add("FromDate", from); }
        if (request.ToDate is { } to) { filters.Add("price_date <= @ToDate"); parameters.Add("ToDate", to); }
        var where = filters.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", filters);
        await using var connection = await connections.OpenConnectionAsync(ct);
        var items = (await connection.QueryAsync<MarketPriceDetailsDto>(new CommandDefinition(
            $"SELECT {Projection} FROM market_prices{where} ORDER BY price_date DESC LIMIT @Limit OFFSET @Offset",
            parameters, cancellationToken: ct))).AsList();
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM market_prices{where}", parameters, cancellationToken: ct));
        return new(items.AsReadOnly(), total, page, size, offset + items.Count < total);
    }

    public async Task<MarketPriceDetailsDto?> UpsertAtomicAsync(UpsertMarketPriceRequest request, Guid actorId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct);
        var sql = request.Version == 0
            ? """
              INSERT INTO market_prices (
                  price_date, ttf_eur_mwh, egsi_etf_eur_mwh, the_eur_mwh, bgo_eur_mwh,
                  pgo_eur_mwh, eua_eur_mwh, within_day_mkt_eur_mwh, eur_sek, eur_chf,
                  eur_gbp, eur_usd, eur_dkk)
              VALUES (
                  @PriceDate, @TtfEurMwh, @EgsiEtfEurMwh, @TheEurMwh, @BgoEurMwh,
                  @PgoEurMwh, @EuaEurMwh, @WithinDayMktEurMwh, @EurSek, @EurChf,
                  @EurGbp, @EurUsd, @EurDkk)
              ON CONFLICT (price_date) DO NOTHING
              RETURNING
              """ + " " + Projection
            : """
              UPDATE market_prices SET
                  ttf_eur_mwh = COALESCE(@TtfEurMwh, ttf_eur_mwh),
                  egsi_etf_eur_mwh = COALESCE(@EgsiEtfEurMwh, egsi_etf_eur_mwh),
                  the_eur_mwh = COALESCE(@TheEurMwh, the_eur_mwh),
                  bgo_eur_mwh = COALESCE(@BgoEurMwh, bgo_eur_mwh),
                  pgo_eur_mwh = COALESCE(@PgoEurMwh, pgo_eur_mwh),
                  eua_eur_mwh = COALESCE(@EuaEurMwh, eua_eur_mwh),
                  within_day_mkt_eur_mwh = COALESCE(@WithinDayMktEurMwh, within_day_mkt_eur_mwh),
                  eur_sek = COALESCE(@EurSek, eur_sek),
                  eur_chf = COALESCE(@EurChf, eur_chf),
                  eur_gbp = COALESCE(@EurGbp, eur_gbp),
                  eur_usd = COALESCE(@EurUsd, eur_usd),
                  eur_dkk = COALESCE(@EurDkk, eur_dkk),
                  version = version + 1
              WHERE price_date = @PriceDate AND version = @Version
              RETURNING
              """ + " " + Projection;
        var result = await connection.QuerySingleOrDefaultAsync<MarketPriceDetailsDto>(new CommandDefinition(
            sql, request, transaction, cancellationToken: ct));
        if (result is null) { await transaction.RollbackAsync(ct); return null; }
        await RepositoryMutation.WriteOutboxAsync(connection, transaction, OutboxAggregateTypes.MarketPrice,
            result.PriceDate.ToString("yyyy-MM-dd"), request.Version == 0 ? "Created" : "Updated",
            result.Version, null, ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<MutationOutcome?> DeleteAtomicAsync(DateOnly priceDate, long version, string reason, Guid actorId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct);
        var deletedVersion = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "DELETE FROM market_prices WHERE price_date = @PriceDate AND version = @Version RETURNING version",
            new { PriceDate = priceDate, Version = version }, transaction, cancellationToken: ct));
        if (deletedVersion is not null)
        {
            await RepositoryMutation.WriteOutboxAsync(connection, transaction, OutboxAggregateTypes.MarketPrice,
                priceDate.ToString("yyyy-MM-dd"), "Deleted", deletedVersion.Value, reason, ct);
            await transaction.CommitAsync(ct);
            return null;
        }
        await transaction.RollbackAsync(ct);
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM market_prices WHERE price_date = @PriceDate)",
            new { PriceDate = priceDate }, cancellationToken: ct));
        return exists ? MutationOutcome.VersionConflict : MutationOutcome.NotFound;
    }
}
