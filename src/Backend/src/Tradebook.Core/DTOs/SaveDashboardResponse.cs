using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Tradebook.Core.Domain.ValueObjects.Ids;

namespace Tradebook.Core.DTOs;

public sealed record SaveDashboardResponse
{
    public SaveDashboardResponse() { }

    [SetsRequiredMembers]
    public SaveDashboardResponse(DashboardId DashboardId, long Version, JsonElement Layout)
    {
        this.DashboardId = DashboardId;
        this.Version = Version;
        this.Layout = Layout;
    }

    public required DashboardId DashboardId { get; init; }

    public required long Version { get; init; }

    public required JsonElement Layout { get; init; }
}
