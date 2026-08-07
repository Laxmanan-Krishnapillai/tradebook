namespace Tradebook.Api.RealTime;

public interface IDashboardPushClient
{
    Task EntityChanged(Guid eventId, long sequenceId, string aggregateType, string aggregateId,
        string eventType, string payloadJson);
}
