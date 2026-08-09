using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record SaveDashboardRequest
{
    public SaveDashboardRequest() { }

    [SetsRequiredMembers]
    public SaveDashboardRequest(Guid DashboardId, long Version, JsonElement Layout)
    {
        this.DashboardId = DashboardId;
        this.Version = Version;
        this.Layout = Layout;
    }

    public required Guid DashboardId { get; init; }

    public required long Version { get; init; }

    [TsType("DashboardSpecification", "../../types/visualizations")]
    public required JsonElement Layout { get; init; }
}
