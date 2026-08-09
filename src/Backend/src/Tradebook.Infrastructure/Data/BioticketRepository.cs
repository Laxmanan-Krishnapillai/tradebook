using System.Data;
using Dapper;
using Tradebook.Core.Domain;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Infrastructure.Data;

public sealed class BioticketRepository(INpgsqlConnectionFactory connections) : IBioticketRepository
{
    private const string Projection = """
        id AS BioticketId, contract_id AS ContractId, contract_instance_id AS ContractInstanceId,
        book_type::text AS BookType, contract_month AS ContractMonth, start_day AS StartDay,
        end_day AS EndDay, volume_nominated_ton AS VolumeNominatedTon,
        volume_realised_ton AS VolumeRealisedTon, volume_ton AS VolumeTon,
        cost_eur_ton AS CostEurTon, revenue_eur AS RevenueEur, vat_pct AS VatPct,
        vat_eur AS VatEur, invoice_amount_eur AS InvoiceAmountEur, status::text AS Status,
        comment AS Comment, version AS Version, created_at AS CreatedAt, updated_at AS UpdatedAt
        """;

    public async Task<BioticketDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var connection = await connections.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await (
                connection.QuerySingleOrDefaultAsync<BioticketDetailsDto>(
                    new CommandDefinition(
                        $"SELECT {Projection} FROM bioticket_deliveries WHERE id = @Id",
                        new { Id = id },
                        cancellationToken: ct
                    )
                )
            ).ConfigureAwait(false);
        }
    }

    public async Task<GetBioticketHistoryResponse> GetHistoryAsync(
        GetBioticketHistoryRequest request,
        CancellationToken ct
    )
    {
        var (page, size, offset) = RepositoryMutation.Page(request.Page, request.PageSize);
        var parameters = new
        {
            Limit = size,
            Offset = offset,
            request.ContractId,
            BookType = string.IsNullOrWhiteSpace(request.BookType) ? null : request.BookType,
            Status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status,
            request.FromMonth,
            request.ToMonth,
        };
        const string rowsSql =
            $"SELECT {Projection} FROM bioticket_deliveries WHERE (@ContractId IS NULL OR contract_id = @ContractId) AND (@BookType IS NULL OR book_type::text = @BookType) AND (@Status IS NULL OR status::text = @Status) AND (@FromMonth IS NULL OR contract_month >= @FromMonth) AND (@ToMonth IS NULL OR contract_month <= @ToMonth) ORDER BY contract_month DESC, contract_instance_id LIMIT @Limit OFFSET @Offset";
        const string countSql =
            "SELECT COUNT(*) FROM bioticket_deliveries WHERE (@ContractId IS NULL OR contract_id = @ContractId) AND (@BookType IS NULL OR book_type::text = @BookType) AND (@Status IS NULL OR status::text = @Status) AND (@FromMonth IS NULL OR contract_month >= @FromMonth) AND (@ToMonth IS NULL OR contract_month <= @ToMonth)";
        var connection = await connections.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var items = (
                await (
                    connection.QueryAsync<BioticketDetailsDto>(
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

    public async Task<BioticketDetailsDto> CreateAtomicAsync(
        CreateBioticketRequest request,
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
                    connection.QuerySingleAsync<BioticketDetailsDto>(
                        new CommandDefinition(
                            """
                            INSERT INTO bioticket_deliveries (
                                contract_id, contract_instance_id, book_type, contract_month, start_day, end_day,
                                volume_nominated_ton, volume_realised_ton, volume_ton, cost_eur_ton,
                                revenue_eur, vat_pct, vat_eur, invoice_amount_eur, status, comment, year)
                            VALUES (
                                @ContractId, @ContractInstanceId, CAST(@BookType AS book_type_enum), @ContractMonth,
                                @StartDay, @EndDay, @VolumeNominatedTon, @VolumeRealisedTon, @VolumeTon,
                                @CostEurTon, @RevenueEur, @VatPct, @VatEur, @InvoiceAmountEur,
                                CAST(COALESCE(@Status, 'Pending - No Invoice') AS report_status_enum), @Comment,
                                EXTRACT(YEAR FROM @ContractMonth::date))
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
                        OutboxAggregateTypes.BioticketDelivery,
                        created.BioticketId.ToString(),
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

    public async Task<BioticketDetailsDto?> UpdateAtomicAsync(
        UpdateBioticketRequest request,
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
                    connection.QuerySingleOrDefaultAsync<BioticketDetailsDto>(
                        new CommandDefinition(
                            """
                            UPDATE bioticket_deliveries SET
                                volume_realised_ton = COALESCE(@VolumeRealisedTon, volume_realised_ton),
                                volume_ton = COALESCE(@VolumeTon, volume_ton), cost_eur_ton = COALESCE(@CostEurTon, cost_eur_ton),
                                revenue_eur = COALESCE(@RevenueEur, revenue_eur), vat_pct = COALESCE(@VatPct, vat_pct),
                                vat_eur = COALESCE(@VatEur, vat_eur),
                                invoice_amount_eur = COALESCE(@InvoiceAmountEur, invoice_amount_eur),
                                status = COALESCE(CAST(@Status AS report_status_enum), status),
                                comment = COALESCE(@Comment, comment), updated_at = clock_timestamp(), version = version + 1
                            WHERE id = @BioticketId AND version = @Version
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
                        OutboxAggregateTypes.BioticketDelivery,
                        updated.BioticketId.ToString(),
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
                            UPDATE bioticket_deliveries SET status = 'Cancelled', updated_at = clock_timestamp(), version = version + 1
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
                    await (
                        RepositoryMutation.WriteOutboxAsync(
                            connection,
                            transaction,
                            OutboxAggregateTypes.BioticketDelivery,
                            id.ToString(),
                            "Cancelled",
                            newVersion.Value,
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
                            "SELECT EXISTS(SELECT 1 FROM bioticket_deliveries WHERE id = @Id)",
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
