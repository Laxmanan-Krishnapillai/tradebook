using Microsoft.AspNetCore.HttpOverrides;

namespace Tradebook.Api.Options;

internal static class NetworkingForwardedHeaders
{
    public static void Configure(
        ForwardedHeadersOptions forwardedHeaders,
        NetworkingOptions networkingOptions
    )
    {
        forwardedHeaders.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        forwardedHeaders.ForwardLimit = 1;
        if (
            NetworkingCidrParser.TryParse(
                networkingOptions.TrustedProxyCidr,
                out var trustedProxyNetwork
            )
        )
            forwardedHeaders.KnownIPNetworks.Add(trustedProxyNetwork);
    }
}
