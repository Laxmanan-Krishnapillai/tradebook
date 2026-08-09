using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using Tradebook.Infrastructure.Data;
using Tradebook.Infrastructure.Migrations;
using Tradebook.Infrastructure.Options;

namespace Tradebook.IntegrationTests;

public abstract class PostgresDatabaseTestBase(PostgresTestFixture fixture)
    : IClassFixture<PostgresTestFixture>,
        IAsyncLifetime
{
    protected PostgresTestFixture Postgres { get; } = fixture;

    protected virtual bool ResetDatabaseBeforeEachTest => true;

    public virtual ValueTask InitializeAsync() =>
        ResetDatabaseBeforeEachTest
            ? new ValueTask(Postgres.ResetDatabaseAsync())
            : ValueTask.CompletedTask;

    public virtual ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
