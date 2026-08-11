using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Tradebook.Api.Options;

internal static class NetworkingServiceCollectionExtensions
{
    public static IServiceCollection AddTradebookNetworking(this IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<NetworkingOptions>, NetworkingOptionsValidator>();
        services
            .AddOptions<NetworkingOptions>()
            .BindConfiguration(NetworkingOptions.SectionName)
            .ValidateOnStart();
        services
            .AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<NetworkingOptions>>(
                (forwardedHeaders, networkingOptions) =>
                    NetworkingForwardedHeaders.Configure(forwardedHeaders, networkingOptions.Value)
            );

        return services;
    }
}
