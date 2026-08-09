using System.Data;
using System.Data.Common;
using Dapper;
using Tradebook.Core.Domain;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;
using Tradebook.Core.Messaging;

namespace Tradebook.Infrastructure.Data;

public sealed class BioticketRepository(
    INpgsqlConnectionFactory connections,
    ITransactionalEventPublisher publisher) : IBioticketRepository
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
        await using var connection = await connections.OpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<BioticketDetailsDto>(new CommandDefinition(
            $"SELECT {Projection} FROM bioticket_deliveries WHERE id = @Id", new { Id = id }, cancellationToken: ct));
    }

    public async Task<GetBioticketHistoryResponse> GetHistoryAsync(GetBioticketHistoryRequest request, CancellationToken ct)
    {
        var (page, size, offset) = RepositoryMutation.Page(request.Page, request.PageSize);
        var filters = new List<string>();
        var parameters = new DynamicParameters(new { Limit = size, Offset = offset });
        if (request.ContractId is { } id) { filters.Add("contract_id = @ContractId"); parameters.Add("ContractId", id); }
        if (!string.IsNullOrWhiteSpace(request.BookType)) { filters.Add("book_type::text = @BookType"); parameters.Add("BookType", request.BookType); }
        if (!string.IsNullOrWhiteSpace(request.Status)) { filters.Add("status::text = @Status"); parameters.Add("Status", request.Status); }
        if (request.FromMonth is { } from) { filters.Add("contract_month >= @FromMonth"); parameters.Add("FromMonth", from); }
        if (request.ToMonth is { } to) { filters.Add("contract_month <= @ToMonth"); parameters.Add("ToMonth", to); }
        var where = filters.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", filters);
        await using var connection = await connections.OpenConnectionAsync(ct);
        var items = (await connection.QueryAsync<BioticketDetailsDto>(new CommandDefinition(
            $"SELECT {Projection} FROM bioticket_deliveries{where} ORDER BY contract_month DESC, contract_instance_id LIMIT @Limit OFFSET @Offset",
            parameters, cancellationToken: ct))).AsList();
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM bioticket_deliveries{where}", parameters, cancellationToken: ct));
        return new(items.AsReadOnly(), total, page, size, offset + items.Count < total);
    }

    public async Task<BioticketDetailsDto> CreateAtomicAsync(CreateBioticketRequest request, Guid actorId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct);
        var created = await connection.QuerySingleAsync<BioticketDetailsDto>(new CommandDefinition("""
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
            """ + " " + Projection, request, transaction, cancellationToken: ct));
        await publisher.EnlistAsync((DbTransaction)transaction, ct);
        await publisher.PublishAsync(EntityChangedDomainEvent.Create(
            RealtimeAggregateTypes.BioticketDelivery, created.BioticketId.ToString(), "Created", created.Version));
        await transaction.CommitAsync(ct);
        await publisher.FlushAsync();
        return created;
    }

    public async Task<BioticketDetailsDto?> UpdateAtomicAsync(UpdateBioticketRequest request, Guid actorId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct);
        var updated = await connection.QuerySingleOrDefaultAsync<BioticketDetailsDto>(new CommandDefinition("""
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
            """ + " " + Projection, request, transaction, cancellationToken: ct));
        if (updated is null) { await transaction.RollbackAsync(ct); return null; }
        await publisher.EnlistAsync((DbTransaction)transaction, ct);
        await publisher.PublishAsync(EntityChangedDomainEvent.Create(
            RealtimeAggregateTypes.BioticketDelivery, updated.BioticketId.ToString(), "Updated", updated.Version));
        await transaction.CommitAsync(ct);
        await publisher.FlushAsync();
        return updated;
    }

    public async Task<MutationOutcome?> CancelAtomicAsync(Guid id, long version, string reason, Guid actorId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct);
        var newVersion = await connection.ExecuteScalarAsync<long?>(new CommandDefinition("""
            UPDATE bioticket_deliveries SET status = 'Cancelled', updated_at = clock_timestamp(), version = version + 1
            WHERE id = @Id AND version = @Version RETURNING version
            """, new { Id = id, Version = version }, transaction, cancellationToken: ct));
        if (newVersion is not null)
        {
            await publisher.EnlistAsync((DbTransaction)transaction, ct);
            await publisher.PublishAsync(EntityChangedDomainEvent.Create(
                RealtimeAggregateTypes.BioticketDelivery, id.ToString(), "Cancelled", newVersion.Value, reason));
            await transaction.CommitAsync(ct);
            await publisher.FlushAsync();
            return null;
        }
        await transaction.RollbackAsync(ct);
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM bioticket_deliveries WHERE id = @Id)", new { Id = id }, cancellationToken: ct));
        return exists ? MutationOutcome.VersionConflict : MutationOutcome.NotFound;
    }
}
