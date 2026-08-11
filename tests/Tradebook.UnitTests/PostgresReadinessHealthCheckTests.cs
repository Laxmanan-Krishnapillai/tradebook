using Microsoft.Extensions.Diagnostics.HealthChecks;
using Tradebook.Api.Features.Health;
using Tradebook.Infrastructure.Migrations;

namespace Tradebook.UnitTests;

public sealed class PostgresReadinessHealthCheckTests
{
    [Fact]
    public async Task ReadinessIsUnhealthyWhileDatabaseInitializationIsPending()
    {
        var check = new PostgresReadinessHealthCheck(null!, new DatabaseInitializationState());

        var result = await check
            .CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Database initialization is still in progress.", result.Description);
    }

    [Fact]
    public async Task ReadinessSurfacesPermanentDatabaseInitializationFailure()
    {
        var state = new DatabaseInitializationState();
        var failure = new InvalidOperationException("broken migration");
        state.MarkFailed(failure);
        var check = new PostgresReadinessHealthCheck(null!, state);

        var result = await check
            .CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Database initialization failed.", result.Description);
        Assert.Same(failure, result.Exception);
    }
}
