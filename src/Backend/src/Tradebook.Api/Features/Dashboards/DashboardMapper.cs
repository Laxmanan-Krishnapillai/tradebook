using System.Text.Json;
using Riok.Mapperly.Abstractions;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.Dashboards;

[Mapper(AutoUserMappings = false, RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class DashboardMapper
{
    [MapProperty(
        nameof(DashboardRow.Layout),
        nameof(SaveDashboardResponse.Layout),
        Use = nameof(ToJsonElement)
    )]
    internal static partial SaveDashboardResponse ToResponse(DashboardRow row, Guid dashboardId);

    private static JsonElement ToJsonElement(string layout)
    {
        using var document = JsonDocument.Parse(layout);
        return document.RootElement.Clone();
    }
}
