using System.Data;
using System.Data.Common;
using Dapper;
using Tradebook.Core.Domain;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;
using Tradebook.Core.Messaging;

namespace Tradebook.Infrastructure.Data;

public sealed class ContractRepository(
    INpgsqlConnectionFactory connections,
    ITransactionalEventPublisher publisher) : IContractRepository
{
    private const string Projection = """
        id AS ContractId, contract_name AS ContractName, counterparty_id AS CounterpartyId,
        product_type::text AS ProductType, action::text AS Action,
        company_shorthand AS CompanyShorthand, country_code AS CountryCode,
        country_dial_code AS CountryDialCode, contract_number AS ContractNumber,
        year_of_contract AS YearOfContract, sourcing_center AS SourcingCenter,
        sales_center AS SalesCenter, balancing_group AS BalancingGroup,
        goo_quality::text AS GooQuality, subsidy_status::text AS SubsidyStatus,
        price_mechanism_gas::text AS PriceMechanismGas,
        fixed_price_gas_eur_mwh AS FixedPriceGasEurMwh,
        contract_type::text AS ContractType, comment AS Comment, is_active AS IsActive,
        version AS Version, created_at AS CreatedAt, updated_at AS UpdatedAt
        """;

    public async Task<ContractDetailsDto?> GetByIdAsync(Guid contractId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<ContractDetailsDto>(new CommandDefinition(
            $"SELECT {Projection} FROM contracts WHERE id = @ContractId",
            new { ContractId = contractId }, cancellationToken: ct));
    }

    public async Task<GetContractHistoryResponse> GetHistoryAsync(GetContractHistoryRequest request, CancellationToken ct)
    {
        var (page, size, offset) = RepositoryMutation.Page(request.Page, request.PageSize);
        var filters = new List<string>();
        var parameters = new DynamicParameters(new { Limit = size, Offset = offset });
        if (request.CounterpartyId is { } counterpartyId) { filters.Add("counterparty_id = @CounterpartyId"); parameters.Add("CounterpartyId", counterpartyId); }
        if (!string.IsNullOrWhiteSpace(request.ProductType)) { filters.Add("product_type::text = @ProductType"); parameters.Add("ProductType", request.ProductType); }
        if (!string.IsNullOrWhiteSpace(request.Action)) { filters.Add("action::text = @Action"); parameters.Add("Action", request.Action); }
        if (request.IsActive is { } isActive) { filters.Add("is_active = @IsActive"); parameters.Add("IsActive", isActive); }
        var where = filters.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", filters);
        await using var connection = await connections.OpenConnectionAsync(ct);
        var items = (await connection.QueryAsync<ContractDetailsDto>(new CommandDefinition(
            $"SELECT {Projection} FROM contracts{where} ORDER BY contract_name LIMIT @Limit OFFSET @Offset",
            parameters, cancellationToken: ct))).AsList();
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM contracts{where}", parameters, cancellationToken: ct));
        return new(items.AsReadOnly(), total, page, size, offset + items.Count < total);
    }

    public async Task<ContractDetailsDto> CreateAtomicAsync(CreateContractRequest request, Guid actorId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct);
        const string insert = """
            INSERT INTO contracts (
                contract_name, counterparty_id, product_type, action, company_shorthand,
                country_code, country_dial_code, contract_number, year_of_contract,
                sourcing_center, sales_center, balancing_group, goo_quality, subsidy_status,
                price_mechanism_gas, fixed_price_gas_eur_mwh, contract_type, comment)
            VALUES (
                @ContractName, @CounterpartyId, CAST(@ProductType AS product_type_enum),
                CAST(@Action AS action_enum), @CompanyShorthand, @CountryCode,
                @CountryDialCode, @ContractNumber, @YearOfContract, @SourcingCenter,
                @SalesCenter, @BalancingGroup, CAST(@GooQuality AS goo_quality_enum),
                CAST(@SubsidyStatus AS subsidy_status_enum),
                CAST(@PriceMechanismGas AS gas_price_mech_enum), @FixedPriceGasEurMwh,
                CAST(COALESCE(@ContractType, 'External') AS contract_type_enum), @Comment)
            RETURNING
            """ + " " + Projection;
        var created = await connection.QuerySingleAsync<ContractDetailsDto>(new CommandDefinition(
            insert, request, transaction, cancellationToken: ct));
        await publisher.EnlistAsync((DbTransaction)transaction, ct);
        await publisher.PublishAsync(EntityChangedDomainEvent.Create(
            RealtimeAggregateTypes.Contract, created.ContractId.ToString(), "Created", created.Version));
        await transaction.CommitAsync(ct);
        await publisher.FlushAsync();
        return created;
    }

    public async Task<ContractDetailsDto?> UpdateAtomicAsync(UpdateContractRequest request, Guid actorId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct);
        var updated = await connection.QuerySingleOrDefaultAsync<ContractDetailsDto>(new CommandDefinition("""
            UPDATE contracts SET
                contract_name = @ContractName, counterparty_id = @CounterpartyId,
                product_type = CAST(@ProductType AS product_type_enum),
                action = CAST(@Action AS action_enum), company_shorthand = @CompanyShorthand,
                country_code = @CountryCode, country_dial_code = @CountryDialCode,
                sourcing_center = @SourcingCenter, sales_center = @SalesCenter,
                balancing_group = @BalancingGroup, goo_quality = CAST(@GooQuality AS goo_quality_enum),
                subsidy_status = CAST(@SubsidyStatus AS subsidy_status_enum),
                price_mechanism_gas = CAST(@PriceMechanismGas AS gas_price_mech_enum),
                fixed_price_gas_eur_mwh = @FixedPriceGasEurMwh,
                contract_type = CAST(COALESCE(@ContractType, 'External') AS contract_type_enum),
                comment = @Comment, is_active = COALESCE(@IsActive, is_active),
                updated_at = clock_timestamp(), version = version + 1
            WHERE id = @ContractId AND version = @Version
            RETURNING
            """ + " " + Projection, request, transaction, cancellationToken: ct));
        if (updated is null) { await transaction.RollbackAsync(ct); return null; }
        await publisher.EnlistAsync((DbTransaction)transaction, ct);
        await publisher.PublishAsync(EntityChangedDomainEvent.Create(
            RealtimeAggregateTypes.Contract, updated.ContractId.ToString(), "Updated", updated.Version));
        await transaction.CommitAsync(ct);
        await publisher.FlushAsync();
        return updated;
    }

    public async Task<MutationOutcome?> DeactivateAtomicAsync(Guid contractId, long version, string reason, Guid actorId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await RepositoryMutation.SetActorAsync(connection, transaction, actorId, ct);
        var newVersion = await connection.ExecuteScalarAsync<long?>(new CommandDefinition("""
            UPDATE contracts SET is_active = FALSE, updated_at = clock_timestamp(), version = version + 1
            WHERE id = @ContractId AND version = @Version RETURNING version
            """, new { ContractId = contractId, Version = version }, transaction, cancellationToken: ct));
        if (newVersion is not null)
        {
            await publisher.EnlistAsync((DbTransaction)transaction, ct);
            await publisher.PublishAsync(EntityChangedDomainEvent.Create(
                RealtimeAggregateTypes.Contract, contractId.ToString(), "Deactivated", newVersion.Value, reason));
            await transaction.CommitAsync(ct);
            await publisher.FlushAsync();
            return null;
        }
        await transaction.RollbackAsync(ct);
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM contracts WHERE id = @Id)", new { Id = contractId }, cancellationToken: ct));
        return exists ? MutationOutcome.VersionConflict : MutationOutcome.NotFound;
    }
}
