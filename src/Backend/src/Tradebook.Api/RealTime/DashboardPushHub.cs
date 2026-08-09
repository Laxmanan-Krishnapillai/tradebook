using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Tradebook.Api.Security;
using Tradebook.Core.Domain;

namespace Tradebook.Api.RealTime;

[Authorize(Policy = "ReadPolicy")]
public sealed class DashboardPushHub : Hub
{
    [HubMethodName("Subscribe")]
    public async Task SubscribeAsync(string group)
    {
        ValidateGroup(group);

        await (Groups.AddToGroupAsync(Context.ConnectionId, group)).ConfigureAwait(false);
    }

    [HubMethodName("Unsubscribe")]
    public Task UnsubscribeAsync(string group)
    {
        ValidateGroup(group);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
    }

    private void ValidateGroup(string group)
    {
        const string entityPrefix = "entity:";
        const string dashboardPrefix = "dashboard:";
        var validEntity =
            group?.StartsWith(entityPrefix, StringComparison.Ordinal) == true
            && OutboxAggregateTypes.IsKnown(group[entityPrefix.Length..])
            && !string.Equals(
                group[entityPrefix.Length..],
                OutboxAggregateTypes.WorkspaceDashboard,
                StringComparison.Ordinal
            );
        var validDashboard =
            group?.StartsWith(dashboardPrefix, StringComparison.Ordinal) == true
            && Guid.TryParse(group[dashboardPrefix.Length..], out var dashboardId)
            && dashboardId == ActorId.From(Context.User!);
        if (!validEntity && !validDashboard)
        {
            throw new HubException($"Unknown subscription group '{group}'.");
        }
    }
}
