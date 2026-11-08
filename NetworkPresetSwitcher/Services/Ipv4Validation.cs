using System.Linq;
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

    /// <summary>
    /// Parses a prefix length written as "/24" or "24" (1-31) and returns the dotted mask.
    /// </summary>
    internal static bool TryConvertPrefixToMask(string value, out string mask)
    {
        mask = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (text.StartsWith('/'))
        {
            text = text[1..].Trim();
        }

        if (text.Length is 0 or > 2 || !text.All(char.IsAsciiDigit) ||
            !int.TryParse(text, out var prefix) || prefix is < 1 or > 31)
        {
            return false;
        }

        var bits = uint.MaxValue << (32 - prefix);
        mask = $"{bits >> 24}.{(bits >> 16) & 0xFF}.{(bits >> 8) & 0xFF}.{bits & 0xFF}";
        return true;
    }

    /// <summary>
    /// Converts a prefix length to a dotted mask; other values are returned trimmed and unchanged.
    /// </summary>
    internal static string NormalizeSubnet(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return TryConvertPrefixToMask(value, out var mask) ? mask : value.Trim();
    }

    /// <summary>Returns the prefix length of a valid dotted mask, or 0 when it is not a valid mask.</summary>
    internal static int GetPrefixLength(string mask)
    {
        if (string.IsNullOrWhiteSpace(mask) || !IsValidSubnetMask(mask) ||
            !IPAddress.TryParse(NormalizeSubnet(mask), out var ip))
        {
            return 0;
        }

        return ip.GetAddressBytes().Sum(b => System.Numerics.BitOperations.PopCount(b));
    }

    internal static bool IsValidSubnetMask(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        value = NormalizeSubnet(value);
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
