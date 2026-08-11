using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Tradebook.Api.Options;

internal sealed class NetworkingOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<NetworkingOptions>
{
    public ValidateOptionsResult Validate(string? name, NetworkingOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.TrustedProxyCidr))
        {
            if (environment.IsProduction())
                failures.Add("Networking:TrustedProxyCidr is required in production.");
        }
        else if (!NetworkingCidrParser.TryParse(options.TrustedProxyCidr, out _))
        {
            failures.Add(
                "Networking:TrustedProxyCidr must be a valid CIDR (for example 10.42.0.0/23)."
            );
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
