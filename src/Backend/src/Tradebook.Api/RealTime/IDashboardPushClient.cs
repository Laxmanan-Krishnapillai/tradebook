namespace Tradebook.Api.RealTime;

/// <summary>Strongly-typed SignalR client surface for realtime entity pushes.</summary>
public interface IDashboardPushClient
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style",
        "VSTHRD200:Use Async suffix",
        Justification = "SignalR client contract / Wolverine handler naming convention."
    )]
    Task EntityChanged(
        Guid eventId,
        long sequenceId,
        string aggregateType,
        string aggregateId,
        string eventType,
        string payloadJson
    );
}
