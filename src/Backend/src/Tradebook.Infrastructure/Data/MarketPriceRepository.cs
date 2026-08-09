using System.Data;
using System.Data.Common;
using System.Globalization;
using Dapper;
using Tradebook.Core.Domain;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;
using Tradebook.Core.Messaging;

namespace Tradebook.Infrastructure.Data;

public sealed class MarketPriceRepository(
    INpgsqlConnectionFactory connections,
    ITransactionalEventPublisher publisher
) : IMarketPriceRepository
{
    private const string Projection = """
        price_date AS PriceDate, ttf_eur_mwh AS TtfEurMwh,
        egsi_etf_eur_mwh AS EgsiEtfEurMwh, the_eur_mwh AS TheEurMwh,
        bgo_eur_mwh AS BgoEurMwh, pgo_eur_mwh AS PgoEurMwh, eua_eur_mwh AS EuaEurMwh,
        within_day_mkt_eur_mwh AS WithinDayMktEurMwh, eur_sek AS EurSek,
        eur_chf AS EurChf, eur_gbp AS EurGbp, eur_usd AS EurUsd, eur_dkk AS EurDkk,
        version AS Version, created_at AS CreatedAt
        """;

    private const string InsertSql =
        """
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
            """
        + " "
        + Projection;

    private const string UpdateSql =
        """
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
            """
        + " "
        + Projection;

    public async Task<MarketPriceDetailsDto?> GetByDateAsync(
        DateOnly priceDate,
        CancellationToken ct
    )
    {
        var connection = await connections.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await (
                connection.QuerySingleOrDefaultAsync<MarketPriceDetailsDto>(
                    new CommandDefinition(
                        $"SELECT {Projection} FROM market_prices WHERE price_date = @PriceDate",
                        new { PriceDate = priceDate },
                        cancellationToken: ct
                    )
                )
            ).ConfigureAwait(false);
        }
    }

    public async Task<GetMarketPriceHistoryResponse> GetHistoryAsync(
        GetMarketPriceHistoryRequest request,
        CancellationToken ct
    )
    {
        var (page, size, offset) = RepositoryMutation.Page(request.Page, request.PageSize, 500);
        var parameters = new
        {
            Limit = size,
            Offset = offset,
            request.FromDate,
            request.ToDate,
        };
        const string rowsSql =
            $"SELECT {Projection} FROM market_prices WHERE (@FromDate IS NULL OR price_date >= @FromDate) AND (@ToDate IS NULL OR price_date <= @ToDate) ORDER BY price_date DESC LIMIT @Limit OFFSET @Offset";
        const string countSql =
            "SELECT COUNT(*) FROM market_prices WHERE (@FromDate IS NULL OR price_date >= @FromDate) AND (@ToDate IS NULL OR price_date <= @ToDate)";
        var connection = await connections.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var items = (
                await (
                    connection.QueryAsync<MarketPriceDetailsDto>(
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

    public async Task<MarketPriceDetailsDto?> UpsertAtomicAsync(
        UpsertMarketPriceRequest request,
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
            await using var transactionLease = transaction.ConfigureAwait(false);
            await (
                RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct)
            ).ConfigureAwait(false);
            var sql = request.Version == 0 ? InsertSql : UpdateSql;
            var result = await (
                connection.QuerySingleOrDefaultAsync<MarketPriceDetailsDto>(
                    new CommandDefinition(sql, request, transaction, cancellationToken: ct)
                )
            ).ConfigureAwait(false);
            if (result is null)
            {
                await (transaction.RollbackAsync(ct)).ConfigureAwait(false);
                return null;
            }
            await publisher.EnlistAsync((DbTransaction)transaction, ct).ConfigureAwait(false);
            await publisher
                .PublishAsync(
                    EntityChangedDomainEvent.Create(
                        RealtimeAggregateTypes.MarketPrice,
                        result.PriceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        request.Version == 0 ? "Created" : "Updated",
                        result.Version
                    )
                )
                .ConfigureAwait(false);
            await publisher.FlushAsync().ConfigureAwait(false);
            await (transaction.CommitAsync(ct)).ConfigureAwait(false);
            return result;
        }
    }

    public async Task<MutationOutcome?> DeleteAtomicAsync(
        DateOnly priceDate,
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
            await using var transactionLease = transaction.ConfigureAwait(false);
            await (
                RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct)
            ).ConfigureAwait(false);
            var deletedVersion = await (
                connection.ExecuteScalarAsync<long?>(
                    new CommandDefinition(
                        "DELETE FROM market_prices WHERE price_date = @PriceDate AND version = @Version RETURNING version",
                        new { PriceDate = priceDate, Version = version },
                        transaction,
                        cancellationToken: ct
                    )
                )
            ).ConfigureAwait(false);
            if (deletedVersion is not null)
            {
                await publisher.EnlistAsync((DbTransaction)transaction, ct).ConfigureAwait(false);
                await publisher
                    .PublishAsync(
                        EntityChangedDomainEvent.Create(
                            RealtimeAggregateTypes.MarketPrice,
                            priceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
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
                        "SELECT EXISTS(SELECT 1 FROM market_prices WHERE price_date = @PriceDate)",
                        new { PriceDate = priceDate },
                        cancellationToken: ct
                    )
                )
            ).ConfigureAwait(false);
            return exists ? MutationOutcome.VersionConflict : MutationOutcome.NotFound;
        }
    }
}
