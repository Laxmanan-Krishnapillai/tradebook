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
    Tradebook.Core.Analytics.SemanticModelLoader semanticModels,
    Tradebook.Infrastructure.Data.INpgsqlConnectionFactory connections,
    Microsoft.Extensions.Hosting.IHostApplicationLifetime lifetime,
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
                await ValidateSchemaOrStopAsync(stoppingToken).ConfigureAwait(false);
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

    private async Task ValidateSchemaOrStopAsync(CancellationToken cancellationToken)
    {
        try
        {
            var connection = await connections
                .OpenConnectionAsync(cancellationToken)
                .ConfigureAwait(false);
            await using var configuredConnection = connection.ConfigureAwait(false);
            await semanticModels
                .ValidateDatabaseSchemaAsync(connection, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A migrated, reachable database with semantic-model drift must fail the
            // process rather than serve wrong analytics (same contract as the old
            // startup-time validation, now sequenced after the async migrations).
            MigrationLog.SchemaDriftFatal(logger, exception);
            lifetime.StopApplication();
        }
    }
}
