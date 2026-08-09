using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tradebook.Infrastructure.Options;

namespace Tradebook.Infrastructure.Migrations;

/// <summary>
/// Applies the embedded DbUp migrations in the background, retrying until PostgreSQL is
/// reachable. Keeps liveness independent from database availability; readiness stays
/// unhealthy until the schema is migrated and validated.
/// </summary>
public sealed class MigrationHostedService(
    IOptions<DatabaseOptions> options,
    ILogger<MigrationHostedService> logger
) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                MigrationRunner.Run(options.Value.ConnectionString);
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                MigrationLog.MigrationDeferred(logger, exception);
            }

            try
            {
                await Task.Delay(RetryDelay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
