using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Tradebook.Core.Interfaces;
using Tradebook.Infrastructure.Outbox;
using MessagePack;
using MessagePack.Resolvers;

namespace Tradebook.Api.RealTime;

public static class SignalRRegistration
{
    public static IServiceCollection AddDashboardPush(this IServiceCollection services)
    {
        services.AddSignalR().AddMessagePackProtocol(options =>
            options.SerializerOptions = MessagePackSerializerOptions.Standard.WithResolver(
                CompositeResolver.Create(VogenMessagePackResolver.Instance, StandardResolver.Instance)));
        services.AddOptions<OutboxOptions>()
            .BindConfiguration("Outbox")
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<IOutboxEventReader, PostgresOutboxEventReader>();
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
