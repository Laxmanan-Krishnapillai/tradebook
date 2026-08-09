using System.Text.Json;
using TypeGen.Core.TypeAnnotations;
using Tradebook.Core.Domain.ValueObjects.Ids;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record SaveDashboardRequest(
    DashboardId DashboardId,
    long Version,
    [property: TsType("DashboardSpecification", "../../types/visualizations")]
    JsonElement Layout);

[ExportTsInterface]
public sealed record SaveDashboardResponse(
    DashboardId DashboardId,
    long Version,
    [property: TsType("DashboardSpecification", "../../types/visualizations")]
    JsonElement Layout);
