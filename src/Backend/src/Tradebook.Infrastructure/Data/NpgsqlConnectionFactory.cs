using System.Globalization;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using Tradebook.Infrastructure.Options;

namespace Tradebook.Infrastructure.Data;

public sealed class NpgsqlConnectionFactory : INpgsqlConnectionFactory, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlConnectionFactory(IOptions<DatabaseOptions> options)
    {
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        _dataSource = NpgsqlDataSource.Create(options.Value.ConnectionString);
    }

    public ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken) =>
        _dataSource.OpenConnectionAsync(cancellationToken);

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();

    private sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override DateOnly Parse(object value) =>
            value switch
            {
                DateOnly date => date,
                DateTime timestamp => DateOnly.FromDateTime(timestamp),
                _ => DateOnly.Parse(value.ToString()!, CultureInfo.InvariantCulture),
            };

        public override void SetValue(System.Data.IDbDataParameter parameter, DateOnly value)
        {
            parameter.DbType = System.Data.DbType.Date;
            parameter.Value = value.ToDateTime(TimeOnly.MinValue);
        }
    }
}
