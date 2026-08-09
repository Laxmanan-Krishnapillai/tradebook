using Microsoft.Extensions.Logging;

namespace Tradebook.Infrastructure.Migrations;

internal static partial class MigrationLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Database migration deferred because PostgreSQL is unavailable; retrying."
    )]
    public static partial void MigrationDeferred(ILogger logger, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Critical,
        Message = "Semantic schema validation failed after migrations; stopping the application."
    )]
    public static partial void SchemaDriftFatal(ILogger logger, Exception exception);
}
