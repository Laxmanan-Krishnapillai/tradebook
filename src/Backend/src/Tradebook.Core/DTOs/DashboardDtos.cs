using System.Text.Json;
using Tradebook.Core.Domain.ValueObjects.Ids;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record SaveDashboardRequest(
    DashboardId DashboardId,
    long Version,
    [property: TsType("DashboardSpecification", "../../types/visualizations")] JsonElement Layout
);

[ExportTsInterface]
public sealed record SaveDashboardResponse(
    DashboardId DashboardId,
    long Version,
    [property: TsType("DashboardSpecification", "../../types/visualizations")] JsonElement Layout
);
