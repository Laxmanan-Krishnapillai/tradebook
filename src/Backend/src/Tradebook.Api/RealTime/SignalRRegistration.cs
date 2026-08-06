using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Tradebook.Core.Interfaces;
using Tradebook.Infrastructure.Outbox;

namespace Tradebook.Api.RealTime;

public static class SignalRRegistration
{
    public static IServiceCollection AddDashboardPush(this IServiceCollection services)
    {
        services.AddSignalR().AddMessagePackProtocol();
        services.AddOptions<OutboxOptions>().BindConfiguration("Outbox");
        services.AddSingleton<IOutboxEventFanout, DashboardPushFanout>();
        services.AddHostedService<OutboxDispatcher>();
        return services;
    }

    public static IEndpointRouteBuilder MapDashboardPushHub(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<DashboardPushHub>("/hubs/dashboard");
        return endpoints;
    }
}
