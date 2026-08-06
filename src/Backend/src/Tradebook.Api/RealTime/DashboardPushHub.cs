using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Tradebook.Api.RealTime;

[Authorize]
public sealed class DashboardPushHub : Hub<IDashboardPushClient>
{
    private static readonly HashSet<string> AllowedGroups = new(StringComparer.Ordinal)
    {
        "entity:PhysicalDelivery", "entity:Contract", "entity:CapacityBooking",
        "entity:GooCertificateTransaction", "entity:MarketPrice", "entity:Hedge"
    };

    public async Task Subscribe(string group)
    {
        if (!AllowedGroups.Contains(group))
            throw new HubException($"Unknown subscription group '{group}'.");

        await Groups.AddToGroupAsync(Context.ConnectionId, group);
    }

    public Task Unsubscribe(string group) => Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
}
