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

    public virtual Task InitializeAsync() =>
        ResetDatabaseBeforeEachTest ? Postgres.ResetDatabaseAsync() : Task.CompletedTask;

    public virtual Task DisposeAsync() => Task.CompletedTask;
}
