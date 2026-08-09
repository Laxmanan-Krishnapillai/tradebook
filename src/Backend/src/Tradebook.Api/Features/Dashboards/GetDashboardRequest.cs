using Dapper;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Infrastructure.Data;

namespace Tradebook.Api.Features.Dashboards;

public sealed record GetDashboardRequest
{
    public required Guid DashboardId { get; init; }
}
