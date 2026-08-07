using System.Text.Json;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record SaveDashboardRequest(Guid DashboardId, long Version, JsonElement Layout);

[ExportTsInterface]
public sealed record SaveDashboardResponse(Guid DashboardId, long Version, JsonElement Layout);
