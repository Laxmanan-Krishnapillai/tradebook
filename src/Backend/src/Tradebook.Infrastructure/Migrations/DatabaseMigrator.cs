using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Extensions.Logging;

namespace Tradebook.Infrastructure.Migrations;

public sealed class DatabaseMigrator(
    Data.INpgsqlConnectionFactory connections,
    ILogger<DatabaseMigrator> logger)
{
    private const string ResourcePrefix = "Tradebook.Database.Migrations.";

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_lock(hashtextextended('tradebook-schema-migrations', 0))",
            cancellationToken: cancellationToken));

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
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
                """, cancellationToken: cancellationToken));

            var applied = (await connection.QueryAsync<AppliedMigration>(new CommandDefinition(
                    "SELECT version AS Version, checksum_sha256 AS Checksum FROM schema_migrations",
                    cancellationToken: cancellationToken)))
                .ToDictionary(row => row.Version, row => row.Checksum.Trim(), StringComparer.Ordinal);

            foreach (var migration in LoadEmbeddedMigrations())
            {
                if (applied.TryGetValue(migration.Version, out var appliedChecksum))
                {
                    if (!string.Equals(appliedChecksum, migration.Checksum, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Migration '{migration.Version}' was modified after it was applied. " +
                            $"Database checksum {appliedChecksum}; embedded checksum {migration.Checksum}.");
                    }

                    continue;
                }

                logger.LogInformation("Applying database migration {Migration}", migration.Version);
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                try
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        migration.Sql,
                        transaction: transaction,
                        commandTimeout: 300,
                        cancellationToken: cancellationToken));
                    await connection.ExecuteAsync(new CommandDefinition("""
                        INSERT INTO schema_migrations (version, checksum_sha256)
                        VALUES (@Version, @Checksum)
                        """, new { migration.Version, migration.Checksum }, transaction,
                        cancellationToken: cancellationToken));
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }
        }
        finally
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "SELECT pg_advisory_unlock(hashtextextended('tradebook-schema-migrations', 0))",
                cancellationToken: CancellationToken.None));
        }
    }

    internal static IReadOnlyList<EmbeddedMigration> LoadEmbeddedMigrations()
    {
        var assembly = typeof(DatabaseMigrator).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) &&
                           name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name =>
            {
                using var stream = assembly.GetManifestResourceStream(name)
                    ?? throw new InvalidOperationException($"Embedded migration '{name}' could not be opened.");
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                var sql = reader.ReadToEnd();
                var version = name[ResourcePrefix.Length..];
                var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))).ToLowerInvariant();
                return new EmbeddedMigration(version, checksum, sql);
            })
            .ToArray();
    }

    private sealed record AppliedMigration(string Version, string Checksum);
    internal sealed record EmbeddedMigration(string Version, string Checksum, string Sql);
}
