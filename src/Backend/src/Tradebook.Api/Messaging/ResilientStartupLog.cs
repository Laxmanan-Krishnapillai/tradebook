namespace Tradebook.Api.Messaging;

internal static partial class ResilientStartupLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Startup of {ServiceName} deferred; retrying until its dependencies are reachable."
    )]
    public static partial void StartDeferred(
        ILogger logger,
        string serviceName,
        Exception exception
    );

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "{ServiceName} started after deferred startup."
    )]
    public static partial void ServiceStarted(ILogger logger, string serviceName);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Shutdown of {ServiceName} faulted; continuing graceful stop."
    )]
    public static partial void StopFaulted(ILogger logger, string serviceName, Exception exception);
}
