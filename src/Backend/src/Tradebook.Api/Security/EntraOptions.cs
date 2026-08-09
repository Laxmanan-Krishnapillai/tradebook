using Microsoft.Extensions.Options;

namespace Tradebook.Api.Security;

public sealed class EntraOptions
{
    public const string SectionName = "Entra";
    public string Instance { get; init; } = "https://login.microsoftonline.com/";
    public string TenantId { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
}
