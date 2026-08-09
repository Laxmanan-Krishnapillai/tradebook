using System.Data;
using Dapper;

namespace Tradebook.Infrastructure.Data;

internal static class RepositoryMutation
{
    public static Task SetActorAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid actorId,
        CancellationToken ct) =>
        connection.ExecuteAsync(new CommandDefinition(
            "SELECT set_config('app.actor_id', @ActorId, true)",
            new { ActorId = actorId.ToString() }, transaction, cancellationToken: ct));

    public static (int Page, int PageSize, int Offset) Page(int page, int pageSize, int maximum = 200)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedSize = Math.Clamp(pageSize, 1, maximum);
        return (normalizedPage, normalizedSize, (normalizedPage - 1) * normalizedSize);
    }
}
