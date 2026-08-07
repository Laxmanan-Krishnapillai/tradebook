using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Tradebook.Infrastructure.Migrations;

public static class DatabaseMigrationHostExtensions
{
    public static Task ApplyTradebookMigrationsAsync(
        this IHost host,
        CancellationToken cancellationToken = default) =>
        host.Services.GetRequiredService<DatabaseMigrator>().MigrateAsync(cancellationToken);
}
