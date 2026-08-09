using System.Text.Json;
using Riok.Mapperly.Abstractions;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.Dashboards;

internal sealed record DashboardRow(string Layout, long Version);
