using DbUp;
using DbUp.Engine;

namespace Tradebook.Infrastructure.Migrations;

/// <summary>Applies the embedded, forward-only database migrations through DbUp.</summary>
public static class MigrationRunner
{
    private const string ResourcePrefix = "Tradebook.Database.Migrations.";

    public static DatabaseUpgradeResult Run(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                typeof(MigrationRunner).Assembly,
                resourceName => resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                    && resourceName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .WithTransactionPerScript()
            .JournalToPostgresqlTable("public", "schema_journal")
            .LogToConsole()
            .Build();

        if (upgrader.GetDiscoveredScripts().Count == 0)
        {
            throw new InvalidOperationException(
                "No embedded migration scripts were discovered; the build is missing src/Database/Migrations.");
        }

        EnsureDatabase.For.PostgresqlDatabase(connectionString);
        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
        {
            throw new InvalidOperationException("Database migration failed.", result.Error);
        }

        return result;
    }
}
