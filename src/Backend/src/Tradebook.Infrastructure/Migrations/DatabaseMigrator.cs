using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Tradebook.Infrastructure.Migrations;

public sealed partial class DatabaseMigrator(
    Data.INpgsqlConnectionFactory connections,
    ILogger<DatabaseMigrator> logger
)
{
    private const string ResourcePrefix = "Tradebook.Database.Migrations.";

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        var connection = await connections
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            await (
                connection.ExecuteAsync(
                    new CommandDefinition(
                        "SELECT pg_advisory_lock(hashtextextended('tradebook-schema-migrations', 0))",
                        cancellationToken: cancellationToken
                    )
                )
            ).ConfigureAwait(false);

            try
            {
                await EnsureMigrationLedgerAsync(connection, cancellationToken)
                    .ConfigureAwait(false);
                await ApplyPendingMigrationsAsync(connection, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                await (
                    connection.ExecuteAsync(
                        new CommandDefinition(
                            "SELECT pg_advisory_unlock(hashtextextended('tradebook-schema-migrations', 0))",
                            cancellationToken: CancellationToken.None
                        )
                    )
                ).ConfigureAwait(false);
            }
        }
    }

    private static async Task EnsureMigrationLedgerAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken
    )
    {
        await (
            connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    CREATE TABLE IF NOT EXISTS schema_migrations (
                        version VARCHAR(255) PRIMARY KEY,
                        checksum_sha256 CHAR(64) NOT NULL CHECK (checksum_sha256 ~ '^[0-9a-f]{64}$'),
                        applied_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp()
                    );

                    DO $migration_ledger_upgrade$
                    BEGIN
                        IF EXISTS (
                            SELECT 1 FROM pg_attribute
                            WHERE attrelid = 'schema_migrations'::regclass
                              AND attname = 'name' AND NOT attisdropped
                        ) AND NOT EXISTS (
                            SELECT 1 FROM pg_attribute
                            WHERE attrelid = 'schema_migrations'::regclass
                              AND attname = 'version' AND NOT attisdropped
                        ) THEN
                            ALTER TABLE schema_migrations RENAME COLUMN name TO version;
                        END IF;

                        IF EXISTS (
                            SELECT 1 FROM pg_attribute
                            WHERE attrelid = 'schema_migrations'::regclass
                              AND attname = 'sha256' AND NOT attisdropped
                        ) AND NOT EXISTS (
                            SELECT 1 FROM pg_attribute
                            WHERE attrelid = 'schema_migrations'::regclass
                              AND attname = 'checksum_sha256' AND NOT attisdropped
                        ) THEN
                            ALTER TABLE schema_migrations RENAME COLUMN sha256 TO checksum_sha256;
                        END IF;
                    END
                    $migration_ledger_upgrade$;
                    """,
                    cancellationToken: cancellationToken
                )
            )
        ).ConfigureAwait(false);
    }

    private async Task ApplyPendingMigrationsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken
    )
    {
        var applied = (
            await connection
                .QueryAsync<AppliedMigration>(
                    new CommandDefinition(
                        "SELECT version AS Version, checksum_sha256 AS Checksum FROM schema_migrations",
                        cancellationToken: cancellationToken
                    )
                )
                .ConfigureAwait(false)
        ).ToDictionary(row => row.Version, row => row.Checksum.Trim(), StringComparer.Ordinal);

        foreach (var migration in LoadEmbeddedMigrations())
        {
            if (applied.TryGetValue(migration.Version, out var appliedChecksum))
            {
                if (!string.Equals(appliedChecksum, migration.Checksum, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Migration '{migration.Version}' was modified after it was applied. "
                            + $"Database checksum {appliedChecksum}; embedded checksum {migration.Checksum}."
                    );
                }

                continue;
            }

            LogApplyingMigration(logger, migration.Version);
            await ApplyMigrationAsync(connection, migration, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task ApplyMigrationAsync(
        NpgsqlConnection connection,
        EmbeddedMigration migration,
        CancellationToken cancellationToken
    )
    {
        var transaction = await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transactionLease = transaction.ConfigureAwait(false);
        try
        {
            await connection
                .ExecuteAsync(
                    new CommandDefinition(
                        migration.Sql,
                        transaction: transaction,
                        commandTimeout: 300,
                        cancellationToken: cancellationToken
                    )
                )
                .ConfigureAwait(false);
            await connection
                .ExecuteAsync(
                    new CommandDefinition(
                        """
                        INSERT INTO schema_migrations (version, checksum_sha256)
                        VALUES (@Version, @Checksum)
                        """,
                        new { migration.Version, migration.Checksum },
                        transaction,
                        cancellationToken: cancellationToken
                    )
                )
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Applying database migration {Migration}"
    )]
    private static partial void LogApplyingMigration(ILogger logger, string migration);

    internal static IReadOnlyList<EmbeddedMigration> LoadEmbeddedMigrations()
    {
        var assembly = typeof(DatabaseMigrator).Assembly;
        return assembly
            .GetManifestResourceNames()
            .Where(name =>
                name.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
            )
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name =>
            {
                using var stream =
                    assembly.GetManifestResourceStream(name)
                    ?? throw new InvalidOperationException(
                        $"Embedded migration '{name}' could not be opened."
                    );
                using var reader = new StreamReader(
                    stream,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true
                );
                var sql = reader.ReadToEnd();
                var version = name[ResourcePrefix.Length..];
                var checksum = Convert
                    .ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)))
                    .ToLowerInvariant();
                return new EmbeddedMigration(version, checksum, sql);
            })
            .ToArray();
    }

    private sealed record AppliedMigration(string Version, string Checksum);

    internal sealed record EmbeddedMigration(string Version, string Checksum, string Sql);
}
