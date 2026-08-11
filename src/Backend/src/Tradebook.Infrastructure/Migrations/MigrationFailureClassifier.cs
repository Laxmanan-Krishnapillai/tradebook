using System.Data.Common;

namespace Tradebook.Infrastructure.Migrations;

internal static class MigrationFailureClassifier
{
    public static bool IsTransient(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            DbException databaseException => databaseException.IsTransient,
            TimeoutException => true,
            _ when exception.InnerException is not null => IsTransient(exception.InnerException),
            _ => false,
        };
    }
}
