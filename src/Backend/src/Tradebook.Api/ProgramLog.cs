namespace Tradebook.Api;

internal static partial class ProgramLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Semantic schema startup validation was deferred because PostgreSQL is unavailable."
    )]
    public static partial void SchemaValidationDeferred(ILogger logger, Exception exception);
}
