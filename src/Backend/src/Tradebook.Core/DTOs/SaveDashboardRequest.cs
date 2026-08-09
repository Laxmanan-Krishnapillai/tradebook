using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Tradebook.Core.Domain.ValueObjects.Ids;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record SaveDashboardRequest
{
    public SaveDashboardRequest() { }

    [SetsRequiredMembers]
    public SaveDashboardRequest(DashboardId DashboardId, long Version, JsonElement Layout)
    {
        this.DashboardId = DashboardId;
        this.Version = Version;
        this.Layout = Layout;
    }

    public required DashboardId DashboardId { get; init; }

    public required long Version { get; init; }

    [TsType("DashboardSpecification", "../../types/visualizations")]
    public required JsonElement Layout { get; init; }
}
