using Microsoft.Extensions.Options;
using Npgsql;
using Tradebook.Infrastructure.Options;

namespace Tradebook.Infrastructure.Data;

public sealed class NpgsqlConnectionFactory : INpgsqlConnectionFactory, IAsyncDisposable
{
    internal const string DataSourceName = "Tradebook";

    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlConnectionFactory(IOptions<DatabaseOptions> options)
    {
        VogenTypeHandlers.RegisterAll();
        var builder = new NpgsqlDataSourceBuilder(options.Value.ConnectionString)
        {
            Name = DataSourceName,
        };
        _dataSource = builder.Build();
    }

    public ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken) =>
        _dataSource.OpenConnectionAsync(cancellationToken);

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
