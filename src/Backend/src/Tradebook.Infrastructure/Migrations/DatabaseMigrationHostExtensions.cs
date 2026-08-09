using DbUp.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Tradebook.Infrastructure.Options;

namespace Tradebook.Infrastructure.Migrations;

public static class DatabaseMigrationHostExtensions
{
    public static DatabaseUpgradeResult ApplyTradebookMigrations(this IHost host)
    {
        var options = host.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        return MigrationRunner.Run(options.ConnectionString);
    }
}
