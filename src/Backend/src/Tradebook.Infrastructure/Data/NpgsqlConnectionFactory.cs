using Microsoft.Extensions.Options;
using Npgsql;
using Dapper;
using Tradebook.Infrastructure.Options;

namespace Tradebook.Infrastructure.Data;

public interface INpgsqlConnectionFactory
{
    ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}

public sealed class NpgsqlConnectionFactory : INpgsqlConnectionFactory, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlConnectionFactory(IOptions<DatabaseOptions> options)
    {
        VogenTypeHandlers.RegisterAll();
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        _dataSource = NpgsqlDataSource.Create(options.Value.ConnectionString);
    }

    public ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken) =>
        _dataSource.OpenConnectionAsync(cancellationToken);

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();

    private sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override DateOnly Parse(object value) => value switch
        {
            DateOnly date => date,
            DateTime timestamp => DateOnly.FromDateTime(timestamp),
            _ => DateOnly.Parse(value.ToString()!)
        };

        public override void SetValue(System.Data.IDbDataParameter parameter, DateOnly value)
        {
            parameter.DbType = System.Data.DbType.Date;
            parameter.Value = value.ToDateTime(TimeOnly.MinValue);
        }
    }
}
