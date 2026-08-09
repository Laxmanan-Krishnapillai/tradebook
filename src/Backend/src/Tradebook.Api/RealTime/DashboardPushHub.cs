using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Tradebook.Api.Security;
using Tradebook.Core.Domain;

namespace Tradebook.Api.RealTime;

[Authorize(Policy = "ReadPolicy")]
public sealed class DashboardPushHub : Hub<IDashboardPushClient>
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style",
        "VSTHRD200:Use Async suffix",
        Justification = "SignalR client contract / Wolverine handler naming convention."
    )]
    public async Task Subscribe(string group)
    {
        ValidateGroup(group);

        await Groups.AddToGroupAsync(Context.ConnectionId, group).ConfigureAwait(false);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style",
        "VSTHRD200:Use Async suffix",
        Justification = "SignalR hub method name is the client-facing contract."
    )]
    public Task Unsubscribe(string group)
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
            && RealtimeAggregateTypes.IsKnown(group[entityPrefix.Length..])
            && !string.Equals(
                group[entityPrefix.Length..],
                RealtimeAggregateTypes.WorkspaceDashboard,
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
