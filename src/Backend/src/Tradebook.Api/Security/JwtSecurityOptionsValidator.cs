using System.Text;
using Microsoft.Extensions.Options;

namespace Tradebook.Api.Security;

internal sealed class JwtSecurityOptionsValidator : IValidateOptions<JwtOptions>
{
    private const string KnownDevelopmentKey = "development-only-signing-key-must-be-replaced";

    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Issuer))
            failures.Add("Jwt:Issuer is required.");
        if (string.IsNullOrWhiteSpace(options.Audience))
            failures.Add("Jwt:Audience is required.");
        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            failures.Add(
                "Jwt:SigningKey is required and must be supplied through secure configuration."
            );
        }
        else
        {
            if (Encoding.UTF8.GetByteCount(options.SigningKey) < 32)
                failures.Add("Jwt:SigningKey must contain at least 32 UTF-8 bytes for HS256.");
            if (string.Equals(options.SigningKey, KnownDevelopmentKey, StringComparison.Ordinal))
                failures.Add("Jwt:SigningKey must not use the known development key.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
