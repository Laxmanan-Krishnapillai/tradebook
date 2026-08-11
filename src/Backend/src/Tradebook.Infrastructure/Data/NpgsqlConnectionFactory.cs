using Microsoft.Extensions.Options;
using Npgsql;
using Tradebook.Infrastructure.Options;

namespace Tradebook.Infrastructure.Data;

public sealed class NpgsqlConnectionFactory : INpgsqlConnectionFactory, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlConnectionFactory(IOptions<DatabaseOptions> options)
    {
        VogenTypeHandlers.RegisterAll();
        _dataSource = NpgsqlDataSource.Create(options.Value.ConnectionString);
    }

    public ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken) =>
        _dataSource.OpenConnectionAsync(cancellationToken);

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
