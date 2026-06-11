using System.Net;

namespace NetworkPresetSwitcher.Services;

internal static class Ipv4Validation
{
    internal static bool IsValidIpv4(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return IPAddress.TryParse(value.Trim(), out var ip) &&
               ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
    }

    internal static bool IsValidIpv4Optional(string value)
    {
        return string.IsNullOrWhiteSpace(value) || IsValidIpv4(value);
    }

    internal static bool IsValidSubnetMask(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!IPAddress.TryParse(value.Trim(), out var ip) ||
            ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = ip.GetAddressBytes();
        uint mask = ((uint)bytes[0] << 24) |
                    ((uint)bytes[1] << 16) |
                    ((uint)bytes[2] << 8) |
                    bytes[3];

        if (mask == 0 || mask == uint.MaxValue)
        {
            return false;
        }

        return (mask | (mask - 1)) == uint.MaxValue;
    }
}
