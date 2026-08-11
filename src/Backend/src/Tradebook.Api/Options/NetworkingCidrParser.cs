using System.Net;

namespace Tradebook.Api.Options;

internal static class NetworkingCidrParser
{
    public static bool TryParse(string? value, out IPNetwork network)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            network = default;
            return false;
        }

        return IPNetwork.TryParse(value.Trim(), out network);
    }
}
