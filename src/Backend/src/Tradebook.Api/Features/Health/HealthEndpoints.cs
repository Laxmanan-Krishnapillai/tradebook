using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Tradebook.Infrastructure.Data;
using Tradebook.Infrastructure.Migrations;

namespace Tradebook.Api.Features.Health;

public static class HealthEndpoints
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";

    public static IServiceCollection AddTradebookHealthChecks(this IServiceCollection services)
    {
        services.AddSingleton<DatabaseInitializationState>();
        services
            .AddHealthChecks()
            .AddCheck<PostgresReadinessHealthCheck>("postgres", tags: ["ready"]);

        return services;
    }

    public static IEndpointRouteBuilder MapTradebookHealthEndpoints(
        this IEndpointRouteBuilder endpoints
    )
    {
        endpoints
            .MapHealthChecks(LivePath, new HealthCheckOptions { Predicate = static _ => false })
            .AllowAnonymous();

        endpoints
            .MapHealthChecks(
                ReadyPath,
                new HealthCheckOptions
                {
                    Predicate = static registration => registration.Tags.Contains("ready"),
                }
            )
            .AllowAnonymous();

        return endpoints;
    }
}
