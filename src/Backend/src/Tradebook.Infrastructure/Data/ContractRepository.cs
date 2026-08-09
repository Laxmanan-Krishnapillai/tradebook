using System.Data;
using Dapper;
using Tradebook.Core.Domain;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Infrastructure.Data;

public sealed class ContractRepository(INpgsqlConnectionFactory connections) : IContractRepository
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

    private const string UpdateSql =
        """
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
            """
        + " "
        + Projection;

    public async Task<ContractDetailsDto?> GetByIdAsync(Guid contractId, CancellationToken ct)
    {
        var connection = await connections.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await (
                connection.QuerySingleOrDefaultAsync<ContractDetailsDto>(
                    new CommandDefinition(
                        $"SELECT {Projection} FROM contracts WHERE id = @ContractId",
                        new { ContractId = contractId },
                        cancellationToken: ct
                    )
                )
            ).ConfigureAwait(false);
        }
    }

    public async Task<GetContractHistoryResponse> GetHistoryAsync(
        GetContractHistoryRequest request,
        CancellationToken ct
    )
    {
        var (page, size, offset) = RepositoryMutation.Page(request.Page, request.PageSize);
        var parameters = new
        {
            Limit = size,
            Offset = offset,
            request.CounterpartyId,
            ProductType = string.IsNullOrWhiteSpace(request.ProductType)
                ? null
                : request.ProductType,
            Action = string.IsNullOrWhiteSpace(request.Action) ? null : request.Action,
            request.IsActive,
        };
        const string rowsSql =
            $"SELECT {Projection} FROM contracts WHERE (@CounterpartyId IS NULL OR counterparty_id = @CounterpartyId) AND (@ProductType IS NULL OR product_type::text = @ProductType) AND (@Action IS NULL OR action::text = @Action) AND (@IsActive IS NULL OR is_active = @IsActive) ORDER BY contract_name LIMIT @Limit OFFSET @Offset";
        const string countSql =
            "SELECT COUNT(*) FROM contracts WHERE (@CounterpartyId IS NULL OR counterparty_id = @CounterpartyId) AND (@ProductType IS NULL OR product_type::text = @ProductType) AND (@Action IS NULL OR action::text = @Action) AND (@IsActive IS NULL OR is_active = @IsActive)";
        var connection = await connections.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var items = (
                await (
                    connection.QueryAsync<ContractDetailsDto>(
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

    public async Task<ContractDetailsDto> CreateAtomicAsync(
        CreateContractRequest request,
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
                const string insert =
                    """
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
                        """
                    + " "
                    + Projection;
                var created = await (
                    connection.QuerySingleAsync<ContractDetailsDto>(
                        new CommandDefinition(insert, request, transaction, cancellationToken: ct)
                    )
                ).ConfigureAwait(false);
                await (
                    RepositoryMutation.WriteOutboxAsync(
                        connection,
                        transaction,
                        OutboxAggregateTypes.Contract,
                        created.ContractId.Value.ToString(),
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

    public async Task<ContractDetailsDto?> UpdateAtomicAsync(
        UpdateContractRequest request,
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
                    connection.QuerySingleOrDefaultAsync<ContractDetailsDto>(
                        new CommandDefinition(
                            UpdateSql,
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
                        OutboxAggregateTypes.Contract,
                        updated.ContractId.Value.ToString(),
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

    public async Task<MutationOutcome?> DeactivateAtomicAsync(
        Guid contractId,
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
                            UPDATE contracts SET is_active = FALSE, updated_at = clock_timestamp(), version = version + 1
                            WHERE id = @ContractId AND version = @Version RETURNING version
                            """,
                            new { ContractId = contractId, Version = version },
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
                            OutboxAggregateTypes.Contract,
                            contractId.ToString(),
                            "Deactivated",
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
                            "SELECT EXISTS(SELECT 1 FROM contracts WHERE id = @Id)",
                            new { Id = contractId },
                            cancellationToken: ct
                        )
                    )
                ).ConfigureAwait(false);
                return exists ? MutationOutcome.VersionConflict : MutationOutcome.NotFound;
            }
        }
    }
}
