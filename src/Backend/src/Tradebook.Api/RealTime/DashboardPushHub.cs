using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Tradebook.Core.Domain;
using Tradebook.Api.Security;

namespace Tradebook.Api.RealTime;

[Authorize(Policy = "ReadPolicy")]
public sealed class DashboardPushHub : Hub<IDashboardPushClient>
{
    public async Task Subscribe(string group)
    {
        ValidateGroup(group);

        await Groups.AddToGroupAsync(Context.ConnectionId, group);
    }

    public Task Unsubscribe(string group)
    {
        ValidateGroup(group);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
    }

    private void ValidateGroup(string group)
    {
        const string entityPrefix = "entity:";
        const string dashboardPrefix = "dashboard:";
        var validEntity = group?.StartsWith(entityPrefix, StringComparison.Ordinal) == true &&
                          RealtimeAggregateTypes.IsKnown(group[entityPrefix.Length..]) &&
                          group[entityPrefix.Length..] != RealtimeAggregateTypes.WorkspaceDashboard;
        var validDashboard = group?.StartsWith(dashboardPrefix, StringComparison.Ordinal) == true &&
                             Guid.TryParse(group[dashboardPrefix.Length..], out var dashboardId) &&
                             dashboardId == ActorId.From(Context.User!);
        if (!validEntity && !validDashboard)
        {
            throw new HubException($"Unknown subscription group '{group}'.");
        }
    }
}
