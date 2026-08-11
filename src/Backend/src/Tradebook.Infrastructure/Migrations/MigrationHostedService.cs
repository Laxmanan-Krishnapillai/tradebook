using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tradebook.Infrastructure.Development;
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
    DevelopmentDataSeeder developmentDataSeeder,
    IHostEnvironment environment,
    Microsoft.Extensions.Hosting.IHostApplicationLifetime lifetime,
    DatabaseInitializationState initialization,
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
                if (environment.IsDevelopment())
                {
                    await developmentDataSeeder
                        .SeedIfEmptyAsync(stoppingToken)
                        .ConfigureAwait(false);
                }
                await ValidateSchemaAsync(stoppingToken).ConfigureAwait(false);
                initialization.MarkReady();
                return;
            }
            catch (Tradebook.Core.Analytics.SemanticSchemaMismatchException exception)
            {
                initialization.MarkFailed(exception);
                MigrationLog.SchemaDriftFatal(logger, exception);
                Environment.ExitCode = 1;
                lifetime.StopApplication();
                return;
            }
            catch (Exception exception)
                when (exception is not OperationCanceledException
                    && MigrationFailureClassifier.IsTransient(exception)
                )
            {
                MigrationLog.MigrationDeferred(logger, exception);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                initialization.MarkFailed(exception);
                MigrationLog.MigrationFatal(logger, exception);
                Environment.ExitCode = 1;
                lifetime.StopApplication();
                return;
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

    private async Task ValidateSchemaAsync(CancellationToken cancellationToken)
    {
        var connection = await connections
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var configuredConnection = connection.ConfigureAwait(false);
        await semanticModels
            .ValidateDatabaseSchemaAsync(connection, cancellationToken)
            .ConfigureAwait(false);
    }
}
