using Microsoft.Extensions.Options;
using Npgsql;
using Tradebook.Infrastructure.Options;

namespace Tradebook.Infrastructure.Data;

public interface INpgsqlConnectionFactory
{
    ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}

public sealed class NpgsqlConnectionFactory : INpgsqlConnectionFactory, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlConnectionFactory(IOptions<DatabaseOptions> options) =>
        _dataSource = NpgsqlDataSource.Create(options.Value.ConnectionString);

    public ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken) =>
        _dataSource.OpenConnectionAsync(cancellationToken);

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
