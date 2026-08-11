namespace Tradebook.Api.Options;

public sealed class NetworkingOptions
{
    public const string SectionName = "Networking";

    public string TrustedProxyCidr { get; init; } = string.Empty;
}
