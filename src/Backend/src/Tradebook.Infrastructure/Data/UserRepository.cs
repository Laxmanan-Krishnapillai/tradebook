using Dapper;
using Tradebook.Core.Domain.Entities;
using Tradebook.Core.Interfaces;

namespace Tradebook.Infrastructure.Data;

public sealed class UserRepository(INpgsqlConnectionFactory connectionFactory) : IUserRepository
{
    public async Task<User?> GetByUsernameAsync(
        string username,
        CancellationToken cancellationToken
    )
    {
        const string sql = """
            SELECT id AS Id, username AS Username, password_hash AS PasswordHash,
                   roles AS Roles, is_active AS IsActive
            FROM users
            WHERE username = @Username;
            """;

        var connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await (
                connection.QuerySingleOrDefaultAsync<User>(
                    new CommandDefinition(
                        sql,
                        new { Username = username },
                        cancellationToken: cancellationToken
                    )
                )
            ).ConfigureAwait(false);
        }
    }
}
