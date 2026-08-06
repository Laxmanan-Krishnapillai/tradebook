using Npgsql;
using Testcontainers.PostgreSql;

namespace Tradebook.IntegrationTests;

public sealed class PostgresTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .WithDatabase("tradebook_test")
        .WithUsername("tradebook")
        .WithPassword("tradebook")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        foreach (var migration in Directory.GetFiles(Path.Combine(RepositoryRoot(), "src", "Database", "Migrations"), "*.sql").OrderBy(Path.GetFileName))
        {
            await using var command = new NpgsqlCommand(await File.ReadAllTextAsync(migration), connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    private static string RepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (Directory.Exists(Path.Combine(current.FullName, ".git"))) return current.FullName;
        throw new DirectoryNotFoundException("Could not locate repository root from the test output directory.");
    }
}
