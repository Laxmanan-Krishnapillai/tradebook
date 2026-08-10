using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Tradebook.Api.Messaging;

public static class WolverineResilienceExtensions
{
    /// <summary>
    /// Rewraps every <see cref="IHostedService"/> registered after
    /// <paramref name="hostedServicesBefore"/> (i.e. by UseWolverine) in
    /// <see cref="ResilientStartupHostedService"/> so an unreachable database defers
    /// Wolverine's startup instead of failing the host.
    /// </summary>
    public static IServiceCollection MakeLateHostedServicesResilient(
        this IServiceCollection services,
        IReadOnlyCollection<ServiceDescriptor> hostedServicesBefore
    )
    {
        var added = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && !hostedServicesBefore.Contains(descriptor)
            )
            .ToList();
        foreach (var descriptor in added)
        {
            services.Remove(descriptor);
            services.AddSingleton<IHostedService>(provider => new ResilientStartupHostedService(
                serviceProvider => CreateInner(serviceProvider, descriptor),
                provider,
                provider.GetRequiredService<ILogger<ResilientStartupHostedService>>()
            ));
        }

        return services;
    }

    private static IHostedService CreateInner(
        IServiceProvider provider,
        ServiceDescriptor descriptor
    )
    {
        if (descriptor.ImplementationInstance is IHostedService instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return (IHostedService)descriptor.ImplementationFactory(provider);
        }

        return (IHostedService)
            ActivatorUtilities.GetServiceOrCreateInstance(
                provider,
                descriptor.ImplementationType
                    ?? throw new InvalidOperationException(
                        "Hosted service descriptor has no implementation."
                    )
            );
    }
}
