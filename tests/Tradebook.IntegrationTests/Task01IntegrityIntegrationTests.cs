using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Tradebook.Infrastructure.Data;
using Tradebook.Infrastructure.Migrations;
using Tradebook.Infrastructure.Options;

namespace Tradebook.IntegrationTests;

public sealed class Task01IntegrityIntegrationTests(PostgresTestFixture postgres)
    : PostgresDatabaseTestBase(postgres)
{
    [Fact]
    public async Task EmbeddedMigrationsAreChecksumTrackedAndIdempotent()
    {
        await using var connections = new NpgsqlConnectionFactory(
            Options.Create(new DatabaseOptions { ConnectionString = Postgres.ConnectionString })
        );
        var migrator = new DatabaseMigrator(connections, NullLogger<DatabaseMigrator>.Instance);

        await migrator.MigrateAsync();
        await migrator.MigrateAsync();

        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        var rows = (
            await connection.QueryAsync<MigrationRow>(
                "SELECT version AS Version, checksum_sha256 AS Checksum FROM schema_migrations ORDER BY version"
            )
        ).ToArray();
        var embeddedCount = typeof(DatabaseMigrator)
            .Assembly.GetManifestResourceNames()
            .Count(name =>
                name.StartsWith("Tradebook.Database.Migrations.", StringComparison.Ordinal)
                && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
            );
        Assert.Equal(embeddedCount, rows.Length);
        Assert.All(rows, row => Assert.Matches("^[0-9a-f]{64}$", row.Checksum));
        Assert.Equal(
            rows.Length,
            rows.Select(row => row.Version).Distinct(StringComparer.Ordinal).Count()
        );
    }

    [Fact]
    public async Task LegacyShellMigrationLedgerIsUpgradedAndReused()
    {
        await using (var connection = new NpgsqlConnection(Postgres.ConnectionString))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync(
                """
                ALTER TABLE schema_migrations RENAME COLUMN version TO name;
                ALTER TABLE schema_migrations RENAME COLUMN checksum_sha256 TO sha256;
                """
            );
        }

        await using var connections = new NpgsqlConnectionFactory(
            Options.Create(new DatabaseOptions { ConnectionString = Postgres.ConnectionString })
        );
        await new DatabaseMigrator(
            connections,
            NullLogger<DatabaseMigrator>.Instance
        ).MigrateAsync();

        await using var verified = new NpgsqlConnection(Postgres.ConnectionString);
        var columns = (
            await verified.QueryAsync<string>(
                """
                SELECT column_name
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'schema_migrations'
                ORDER BY column_name
                """
            )
        ).ToArray();
        Assert.Contains("version", columns, StringComparer.Ordinal);
        Assert.Contains("checksum_sha256", columns, StringComparer.Ordinal);
        Assert.DoesNotContain("name", columns, StringComparer.Ordinal);
        Assert.DoesNotContain("sha256", columns, StringComparer.Ordinal);
        var embeddedCount = typeof(DatabaseMigrator)
            .Assembly.GetManifestResourceNames()
            .Count(name =>
                name.StartsWith("Tradebook.Database.Migrations.", StringComparison.Ordinal)
                && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
            );
        Assert.Equal(
            embeddedCount,
            await verified.ExecuteScalarAsync<int>("SELECT count(*) FROM schema_migrations")
        );
    }

    [Fact]
    public async Task DeliveryInstanceIsCanonicalAndInvalidMonthOrInstanceIsRejected()
    {
        var (contractId, contractName) = await CreateContractAsync();
        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await connection.OpenAsync();

        var generated = await connection.ExecuteScalarAsync<string>(
            """
            INSERT INTO physical_deliveries (contract_id, book_type, supply_month)
            VALUES (@ContractId, 'Sales', DATE '2026-08-01')
            RETURNING contract_instance_id
            """,
            new { ContractId = contractId }
        );
        Assert.Equal($"{contractName}-8-2026", generated);

        var mismatch = await Assert.ThrowsAsync<PostgresException>(() =>
            connection.ExecuteAsync(
                """
                INSERT INTO capacity_bookings (contract_id, contract_instance_id, supply_month)
                VALUES (@ContractId, 'tampered-instance', DATE '2026-09-01')
                """,
                new { ContractId = contractId }
            )
        );
        Assert.Equal(PostgresErrorCodes.CheckViolation, mismatch.SqlState);
        Assert.Equal("ck_contract_instance_matches_month", mismatch.ConstraintName);

        var invalidMonth = await Assert.ThrowsAsync<PostgresException>(() =>
            connection.ExecuteAsync(
                """
                INSERT INTO transfers (contract_id, supply_month)
                VALUES (@ContractId, DATE '2026-09-02')
                """,
                new { ContractId = contractId }
            )
        );
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalidMonth.SqlState);
    }

    [Fact]
    public async Task AuditSystemRangesAreContiguousAndCommitChainIsLinked()
    {
        var (contractId, _) = await CreateContractAsync();
        var actorId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await connection.OpenAsync();
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await connection.ExecuteAsync(
                "SELECT set_config('app.actor_id', @ActorId, true)",
                new { ActorId = actorId.ToString() },
                transaction
            );
            await connection.ExecuteAsync(
                """
                INSERT INTO physical_deliveries (id, contract_id, book_type, supply_month, volume_mwh)
                VALUES (@DeliveryId, @ContractId, 'Sales', DATE '2026-10-01', 10)
                """,
                new { DeliveryId = deliveryId, ContractId = contractId },
                transaction
            );
            await connection.ExecuteAsync(
                "UPDATE physical_deliveries SET volume_mwh = 12, version = version + 1 WHERE id = @DeliveryId",
                new { DeliveryId = deliveryId },
                transaction
            );
            await transaction.CommitAsync();
        }

        var audit = (
            await connection.QueryAsync<AuditPeriod>(
                """
                SELECT lower(system_time) AS Start, upper(system_time) AS End,
                       actor_id AS ActorId, commit_hash AS CommitHash,
                       parent_commit_hash AS ParentCommitHash
                FROM audit_log
                WHERE entity_name = 'physical_deliveries' AND entity_id = @EntityId
                ORDER BY lower(system_time)
                """,
                new { EntityId = deliveryId.ToString() }
            )
        ).ToArray();

        Assert.Equal(2, audit.Length);
        Assert.Equal(audit[0].End, audit[1].Start);
        Assert.Null(audit[0].ParentCommitHash);
        Assert.Equal(audit[0].CommitHash, audit[1].ParentCommitHash);
        Assert.All(audit, item => Assert.Equal(actorId, item.ActorId));
    }

    [Fact]
    public async Task AuditExclusionConstraintRejectsOverlappingRangesWith23P01()
    {
        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await connection.OpenAsync();
        const string insert = """
            INSERT INTO audit_log
                (entity_name, entity_id, actor_id, operation, system_time, valid_time,
                 diff_patch, commit_hash)
            VALUES
                (@EntityName, @EntityId, @ActorId, @Operation,
                 tstzrange(@SystemStart, @SystemEnd, '[)'),
                 tstzrange(@ValidStart, @ValidEnd, '[)'),
                 CAST(@DiffPatch AS jsonb), @CommitHash)
            """;
        var entityId = Guid.NewGuid().ToString();
        var actorId = Guid.NewGuid();

        await connection.ExecuteAsync(
            insert,
            CreateAuditInsertParameters(
                entityId,
                actorId,
                Utc(2026, 1, 1),
                Utc(2026, 3, 1),
                Utc(2026, 1, 1),
                Utc(2027, 1, 1),
                'a'
            )
        );

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            connection.ExecuteAsync(
                insert,
                CreateAuditInsertParameters(
                    entityId,
                    actorId,
                    Utc(2026, 2, 1),
                    Utc(2026, 4, 1),
                    Utc(2026, 6, 1),
                    Utc(2027, 6, 1),
                    'b'
                )
            )
        );

        Assert.Equal("23P01", exception.SqlState);
        Assert.Equal(PostgresErrorCodes.ExclusionViolation, exception.SqlState);
        Assert.Equal(
            1,
            await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM audit_log WHERE entity_name = @EntityName AND entity_id = @EntityId",
                new { EntityName = "acceptance_entity", EntityId = entityId }
            )
        );
    }

    [Fact]
    public async Task GetEntityStateAsOfReconstructsTheExactHistoricalState()
    {
        var (contractId, _) = await CreateContractAsync();
        var deliveryId = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            "SELECT set_config('app.actor_id', @ActorId, false)",
            new { ActorId = Guid.NewGuid().ToString() }
        );
        await connection.ExecuteAsync(
            """
            INSERT INTO physical_deliveries
                (id, contract_id, book_type, supply_month, volume_mwh)
            VALUES
                (@DeliveryId, @ContractId, 'Sales', DATE '2026-10-01', 10)
            """,
            new { DeliveryId = deliveryId, ContractId = contractId }
        );

        var expected = await connection.QuerySingleAsync<HistoricalAuditSnapshot>(
            """
            SELECT lower(system_time) AS SystemTime,
                   lower(valid_time) AS ValidTime,
                   post_state::text AS PostState
            FROM audit_log
            WHERE entity_name = 'physical_deliveries'
              AND entity_id = @EntityId
              AND operation = 'INSERT'
            """,
            new { EntityId = deliveryId.ToString() }
        );

        await connection.ExecuteAsync(
            """
            UPDATE physical_deliveries
               SET volume_mwh = 99,
                   status = 'Issue',
                   version = version + 1,
                   updated_at = clock_timestamp()
             WHERE id = @DeliveryId
            """,
            new { DeliveryId = deliveryId }
        );

        await AssertHistoricalStateAsync(connection, deliveryId, expected);
        Assert.Equal(
            (99m, "Issue", 2L),
            await connection.QuerySingleAsync<(decimal Volume, string Status, long Version)>(
                """
                SELECT volume_mwh AS Volume, status::text AS Status, version AS Version
                FROM physical_deliveries
                WHERE id = @DeliveryId
                """,
                new { DeliveryId = deliveryId }
            )
        );
    }

    private static async Task AssertHistoricalStateAsync(
        NpgsqlConnection connection,
        Guid deliveryId,
        HistoricalAuditSnapshot expected
    )
    {
        var exactMatch = await connection
            .ExecuteScalarAsync<bool>(
                """
                SELECT get_entity_state_as_of(
                           'physical_deliveries', @EntityId, @SystemTime, @ValidTime)
                       = CAST(@ExpectedState AS jsonb)
                """,
                new
                {
                    EntityId = deliveryId.ToString(),
                    expected.SystemTime,
                    expected.ValidTime,
                    ExpectedState = expected.PostState,
                }
            )
            .ConfigureAwait(false);
        var reconstructed = await connection
            .ExecuteScalarAsync<string>(
                """
                SELECT get_entity_state_as_of(
                           'physical_deliveries', @EntityId, @SystemTime, @ValidTime)::text
                """,
                new
                {
                    EntityId = deliveryId.ToString(),
                    expected.SystemTime,
                    expected.ValidTime,
                }
            )
            .ConfigureAwait(false);

        Assert.True(exactMatch);
        Assert.NotNull(reconstructed);
        using var state = System.Text.Json.JsonDocument.Parse(reconstructed);
        Assert.Equal(deliveryId, state.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(10m, state.RootElement.GetProperty("volume_mwh").GetDecimal());
        Assert.Equal("Pending - No Invoice", state.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, state.RootElement.GetProperty("version").GetInt64());
    }

    [Fact]
    public async Task MarketPricesMonthlyReturnsExactAveragesFor90DailyRows()
    {
        var seeded = CreateDailyMarketPrices(new DateOnly(2026, 1, 1));

        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO market_prices
                (price_date, ttf_eur_mwh, egsi_etf_eur_mwh, the_eur_mwh,
                 eur_sek, eur_chf, eur_dkk)
            VALUES
                (@PriceDate, @Ttf, @EgsiEtf, @The, @EurSek, @EurChf, @EurDkk)
            """,
            seeded
        );

        var actual = (
            await connection.QueryAsync<MonthlyMarketAverage>(
                """
                SELECT month AS Month,
                       avg_ttf_eur_mwh AS Ttf,
                       avg_egsi_etf_eur_mwh AS EgsiEtf,
                       avg_the_eur_mwh AS The,
                       avg_eur_sek AS EurSek,
                       avg_eur_chf AS EurChf,
                       avg_eur_dkk AS EurDkk
                FROM market_prices_monthly
                ORDER BY month
                """
            )
        ).ToArray();
        var expected = seeded
            .GroupBy(price => new DateOnly(price.PriceDate.Year, price.PriceDate.Month, 1))
            .Select(group => new MonthlyMarketAverage(
                group.Key,
                group.Average(price => price.Ttf),
                group.Average(price => price.EgsiEtf),
                group.Average(price => price.The),
                group.Average(price => price.EurSek),
                group.Average(price => price.EurChf),
                group.Average(price => price.EurDkk)
            ))
            .OrderBy(row => row.Month)
            .ToArray();

        Assert.Equal(
            90,
            await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM market_prices")
        );
        Assert.Equal(
            [new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 1)],
            actual.Select(row => row.Month)
        );
        Assert.Equal(expected, actual);
    }

    private static DailyMarketPrice[] CreateDailyMarketPrices(DateOnly start) =>
        Enumerable
            .Range(0, 90)
            .Select(offset =>
            {
                var ordinal = offset + 1m;
                return new DailyMarketPrice(
                    start.AddDays(offset),
                    ordinal,
                    ordinal * 2m + 0.25m,
                    100m + ordinal / 10m,
                    7m + ordinal / 100m,
                    1m + ordinal / 1000m,
                    7.4m + ordinal / 10_000m
                );
            })
            .ToArray();

    [Fact]
    public async Task MonthlyReportingViewJoinsContractAndCounterpartyDimensions()
    {
        var (contractId, contractName) = await CreateContractAsync();
        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO physical_deliveries
                (contract_id, book_type, supply_month, volume_mwh, revenue_eur, tax_eur, vat_eur, invoice_amount_eur)
            VALUES (@ContractId, 'Sales', DATE '2026-11-01', 25, 1000, 50, 262.50, 1312.50)
            """,
            new { ContractId = contractId }
        );
        await connection.ExecuteAsync("REFRESH MATERIALIZED VIEW delivery_monthly_summary");

        var row = await connection.QuerySingleAsync<MonthlySummary>(
            """
            SELECT contract_name AS ContractName, product_type::text AS ProductType,
                   action::text AS Action, counterparty_name AS CounterpartyName,
                   volume_mwh AS VolumeMwh, invoice_amount_eur AS InvoiceAmountEur
            FROM delivery_monthly_summary
            WHERE contract_name = @ContractName AND supply_month = DATE '2026-11-01'
            """,
            new { ContractName = contractName }
        );

        Assert.Equal(contractName, row.ContractName);
        Assert.Equal("Gas", row.ProductType);
        Assert.Equal("Sell", row.Action);
        Assert.StartsWith("Counterparty-", row.CounterpartyName, StringComparison.Ordinal);
        Assert.Equal(25m, row.VolumeMwh);
        Assert.Equal(1312.50m, row.InvoiceAmountEur);
    }

    [Fact]
    public async Task ContractSubsidySuffixCannotDisagreeWithStatus()
    {
        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await connection.OpenAsync();
        var counterpartyId = await CreateCounterpartyAsync(connection);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            connection.ExecuteAsync(
                """
                INSERT INTO contracts (contract_name, counterparty_id, product_type, action, subsidy_status)
                VALUES ('MISMATCH45.SG.2601.NOQS', @CounterpartyId, 'Gas', 'Sell', 'UNS')
                """,
                new { CounterpartyId = counterpartyId }
            )
        );

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_contracts_subsidy_suffix", exception.ConstraintName);
    }

    private async Task<(Guid Id, string Name)> CreateContractAsync()
    {
        var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            var counterpartyId = await CreateCounterpartyAsync(connection).ConfigureAwait(false);
            var contractId = Guid.NewGuid();
            var contractName = $"TEST45.SG.{contractId:N}.NOQS";
            await connection
                .ExecuteAsync(
                    """
                    INSERT INTO contracts (id, contract_name, counterparty_id, product_type, action, subsidy_status)
                    VALUES (@Id, @Name, @CounterpartyId, 'Gas', 'Sell', 'SUB')
                    """,
                    new
                    {
                        Id = contractId,
                        Name = contractName,
                        CounterpartyId = counterpartyId,
                    }
                )
                .ConfigureAwait(false);
            return (contractId, contractName);
        }
    }

    private static async Task<Guid> CreateCounterpartyAsync(NpgsqlConnection connection)
    {
        var id = Guid.NewGuid();
        await connection
            .ExecuteAsync(
                "INSERT INTO counterparties (id, name, shorthand) VALUES (@Id, @Name, @Shorthand)",
                new
                {
                    Id = id,
                    Name = $"Counterparty-{id:N}",
                    Shorthand = $"CP{id:N}"[..20],
                }
            )
            .ConfigureAwait(false);
        return id;
    }

    private static AuditInsertParameters CreateAuditInsertParameters(
        string entityId,
        Guid actorId,
        DateTime systemStart,
        DateTime systemEnd,
        DateTime validStart,
        DateTime validEnd,
        char commitHashCharacter
    ) =>
        new(
            "acceptance_entity",
            entityId,
            actorId,
            "INSERT",
            systemStart,
            systemEnd,
            validStart,
            validEnd,
            "[]",
            new string(commitHashCharacter, 64)
        );

    private sealed record MigrationRow(string Version, string Checksum);

    private sealed record AuditInsertParameters(
        string EntityName,
        string EntityId,
        Guid ActorId,
        string Operation,
        DateTime SystemStart,
        DateTime SystemEnd,
        DateTime ValidStart,
        DateTime ValidEnd,
        string DiffPatch,
        string CommitHash
    );

    private sealed record AuditPeriod(
        DateTime Start,
        DateTime? End,
        Guid ActorId,
        string CommitHash,
        string? ParentCommitHash
    );

    private sealed record HistoricalAuditSnapshot(
        DateTime SystemTime,
        DateTime ValidTime,
        string PostState
    );

    private sealed record DailyMarketPrice(
        DateOnly PriceDate,
        decimal Ttf,
        decimal EgsiEtf,
        decimal The,
        decimal EurSek,
        decimal EurChf,
        decimal EurDkk
    );

    private sealed record MonthlyMarketAverage(
        DateOnly Month,
        decimal Ttf,
        decimal EgsiEtf,
        decimal The,
        decimal EurSek,
        decimal EurChf,
        decimal EurDkk
    );

    private sealed record MonthlySummary(
        string ContractName,
        string ProductType,
        string Action,
        string CounterpartyName,
        decimal VolumeMwh,
        decimal InvoiceAmountEur
    );

    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
