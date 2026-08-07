using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Tradebook.Infrastructure.Data;

namespace Tradebook.Api.Features.Health;

public static class HealthEndpoints
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";

    public static IServiceCollection AddTradebookHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<PostgresReadinessHealthCheck>("postgres", tags: ["ready"]);

        return services;
    }

    public static IEndpointRouteBuilder MapTradebookHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(LivePath, new HealthCheckOptions
        {
            Predicate = static _ => false
        }).AllowAnonymous();

        endpoints.MapHealthChecks(ReadyPath, new HealthCheckOptions
        {
            Predicate = static registration => registration.Tags.Contains("ready")
        }).AllowAnonymous();

        return endpoints;
    }
}

internal sealed class PostgresReadinessHealthCheck(INpgsqlConnectionFactory connections) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await connections.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            var result = await command.ExecuteScalarAsync(cancellationToken);

            return result is 1
                ? HealthCheckResult.Healthy("PostgreSQL is reachable.")
                : HealthCheckResult.Unhealthy("PostgreSQL readiness query returned an unexpected result.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is not reachable.", exception);
        }
    }
}
