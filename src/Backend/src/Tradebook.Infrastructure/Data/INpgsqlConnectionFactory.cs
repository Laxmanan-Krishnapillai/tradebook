using Npgsql;

namespace Tradebook.Infrastructure.Data;

public interface INpgsqlConnectionFactory
{
    ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}
