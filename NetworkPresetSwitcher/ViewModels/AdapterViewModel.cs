using System;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using NetworkPresetSwitcher.Infrastructure;

namespace NetworkPresetSwitcher.ViewModels;

public sealed class AdapterViewModel : ObservableObject
{
    private const string Separator = " / ";
    private readonly string _deviceName;
    private readonly string _manufacturer;

    public AdapterViewModel(NetworkInterface adapter)
    {
        Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        (_deviceName, _manufacturer) = LoadDeviceInfo(adapter);
    }

    public NetworkInterface Adapter { get; }

    public string Id => Adapter.Id;

    public string Name => Adapter.Name;

    public string Description => Adapter.Description;

    public string DeviceName => _deviceName;

    public string Manufacturer => _manufacturer;

    public string DisplayName => $"{Adapter.Name} ({TypeName})";

    public string TypeName => Adapter.NetworkInterfaceType switch
    {
        NetworkInterfaceType.Ethernet => L("Adapter.Type.Ethernet"),
        NetworkInterfaceType.Wireless80211 => L("Adapter.Type.Wireless"),
        NetworkInterfaceType.Loopback => L("Adapter.Type.Loopback"),
        NetworkInterfaceType.Ppp => L("Adapter.Type.Ppp"),
        NetworkInterfaceType.Tunnel => L("Adapter.Type.Tunnel"),
        _ => Adapter.NetworkInterfaceType.ToString()
    };

    public string StatusBadge => Adapter.OperationalStatus == OperationalStatus.Up
        ? L("Status.Connected")
        : L("Status.Disconnected");

    public string ModeBadge => IsDhcp ? L("Mode.Dhcp") : L("Mode.Static");

    public string IpBadge
    {
        get
        {
            var ipv4 = GetIpv4Info();
            return ipv4 == null ? L("Label.Ipv4NotSet") : $"IPv4 {ipv4.Value.Address} / {ipv4.Value.Mask}";
        }
    }

    public string Ipv4Address
    {
        get
        {
            var ipv4 = GetIpv4Info();
            return ipv4?.Address ?? string.Empty;
        }
    }

    public string GatewayBadge => string.IsNullOrWhiteSpace(Gateway) ? L("Label.GwNotSet") : $"GW {Gateway}";

    public string DnsBadge => string.IsNullOrWhiteSpace(DnsServers) ? L("Label.DnsNotSet") : $"DNS {DnsServers}";

    public bool IsDhcp
    {
        get
        {
            try
            {
                var properties = Adapter.GetIPProperties();
                return properties.DhcpServerAddresses.Count > 0;
            }
            catch
            {
                return false;
            }
        }
    }

    public string SpeedText
    {
        get
        {
            var speed = Adapter.Speed;
            if (speed < 0)
            {
                return L("Text.NotSet");
            }

            if (speed >= 1_000_000_000)
            {
                return $"{speed / 1_000_000_000.0:F1} Gbps";
            }
            if (speed >= 1_000_000)
            {
                return $"{speed / 1_000_000.0:F1} Mbps";
            }
            if (speed >= 1_000)
            {
                return $"{speed / 1_000.0:F1} Kbps";
            }

            return $"{speed} bps";
        }
    }

    public string MacAddress
    {
        get
        {
            var mac = Adapter.GetPhysicalAddress();
            if (mac == null || mac.GetAddressBytes().Length == 0)
            {
                return L("Text.NotSet");
            }

            return BitConverter.ToString(mac.GetAddressBytes()).Replace("-", ":");
        }
    }

    public string Gateway
    {
        get
        {
            try
            {
                var properties = Adapter.GetIPProperties();
                var gateway = properties.GatewayAddresses
                    .FirstOrDefault(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                return gateway?.Address.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    public string DnsServers
    {
        get
        {
            try
            {
                var properties = Adapter.GetIPProperties();
                var dnsList = properties.DnsAddresses
                    .Where(d => d.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    .Select(d => d.ToString())
                    .ToList();
                return string.Join(", ", dnsList);
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    public string DetailLine => $"{TypeName}{Separator}{SpeedText}{Separator}MAC {MacAddress}";

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(TypeName));
        OnPropertyChanged(nameof(StatusBadge));
        OnPropertyChanged(nameof(ModeBadge));
        OnPropertyChanged(nameof(IpBadge));
        OnPropertyChanged(nameof(Ipv4Address));
        OnPropertyChanged(nameof(GatewayBadge));
        OnPropertyChanged(nameof(DnsBadge));
        OnPropertyChanged(nameof(SpeedText));
        OnPropertyChanged(nameof(MacAddress));
        OnPropertyChanged(nameof(DetailLine));
    }

    private (string Address, string Mask)? GetIpv4Info()
    {
        try
        {
            var properties = Adapter.GetIPProperties();
            var ipv4 = properties.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            if (ipv4 == null)
            {
                return null;
            }

            return (ipv4.Address.ToString(), ipv4.IPv4Mask?.ToString() ?? string.Empty);
        }
        catch
        {
            return null;
        }
    }

    private static string L(string key) => Localization.T(key);

    private static (string Name, string Manufacturer) LoadDeviceInfo(NetworkInterface adapter)
    {
        try
        {
            var guid = adapter.Id;
            if (!string.IsNullOrWhiteSpace(guid))
            {
                var query = $"SELECT Name, Manufacturer FROM Win32_NetworkAdapter WHERE GUID = '{guid}'";
                using var searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject mo in searcher.Get())
                {
                    var name = mo["Name"] as string ?? string.Empty;
                    var manufacturer = mo["Manufacturer"] as string ?? string.Empty;
                    return (string.IsNullOrWhiteSpace(name) ? adapter.Description : name,
                        string.IsNullOrWhiteSpace(manufacturer) ? L("Text.NotSet") : manufacturer);
                }
            }
        }
        catch
        {
        }

        return (adapter.Description, L("Text.NotSet"));
    }
}

