using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Tradebook.Infrastructure.Data;

namespace Tradebook.Api.Features.Health;

internal sealed class PostgresReadinessHealthCheck(INpgsqlConnectionFactory connections)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var connection = await (
                connections.OpenConnectionAsync(cancellationToken)
            ).ConfigureAwait(false);
            await using var configuredConnection = connection.ConfigureAwait(false);
            var command = connection.CreateCommand();
            await using var configuredCommand = command.ConfigureAwait(false);
            command.CommandText = "SELECT 1";
            var result = await (command.ExecuteScalarAsync(cancellationToken)).ConfigureAwait(
                false
            );

            return result is 1
                ? HealthCheckResult.Healthy("PostgreSQL is reachable.")
                : HealthCheckResult.Unhealthy(
                    "PostgreSQL readiness query returned an unexpected result."
                );
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException
                || !cancellationToken.IsCancellationRequested
            )
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is not reachable.", exception);
        }
    }
}
