using Microsoft.Extensions.Options;

namespace Tradebook.Api.Security;

internal sealed class EntraOptionsValidator : IValidateOptions<EntraOptions>
{
    public ValidateOptionsResult Validate(string? name, EntraOptions options)
    {
        var errors = new List<string>();
        if (
            !string.Equals(
                options.Instance,
                "https://login.microsoftonline.com/",
                StringComparison.Ordinal
            )
        )
            errors.Add("Entra:Instance must be the Microsoft single-tenant authority.");
        if (!Guid.TryParse(options.TenantId, out var tenant) || tenant == Guid.Empty)
            errors.Add("Entra:TenantId must be a non-placeholder UUID.");
        if (!Guid.TryParse(options.ClientId, out var client) || client == Guid.Empty)
            errors.Add("Entra:ClientId must be a non-placeholder UUID.");
        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
