using Microsoft.Extensions.Options;

namespace Tradebook.Api.AgentTools;

internal sealed class InAppAgentOptionsValidator : IValidateOptions<InAppAgentOptions>
{
    public ValidateOptionsResult Validate(string? name, InAppAgentOptions options)
    {
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        var errors = new List<string>();
        if (
            !Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint)
            || !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
        )
            errors.Add("InAppAgent:Endpoint must be an absolute HTTPS Azure OpenAI endpoint.");
        if (string.IsNullOrWhiteSpace(options.DeploymentName))
            errors.Add("InAppAgent:DeploymentName is required when the agent is enabled.");
        if (
            options.ManagedIdentityClientId is not null
            && (
                !Guid.TryParse(options.ManagedIdentityClientId, out var clientId)
                || clientId == Guid.Empty
            )
        )
            errors.Add("InAppAgent:ManagedIdentityClientId must be a non-placeholder UUID.");

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
