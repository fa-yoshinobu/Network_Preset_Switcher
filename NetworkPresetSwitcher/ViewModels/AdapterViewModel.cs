using System;
using System.Collections.Generic;
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

    public AdapterViewModel(NetworkInterface adapter, IReadOnlyDictionary<string, (string Name, string Manufacturer)>? deviceInfo = null)
    {
        Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        (_deviceName, _manufacturer) = ResolveDeviceInfo(adapter, deviceInfo);
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

    /// <summary>"Connected", "Disconnected" or "Unknown"; drives the status badge colour.</summary>
    public string StatusKind
    {
        get
        {
            try
            {
                return Adapter.OperationalStatus switch
                {
                    OperationalStatus.Up => "Connected",
                    OperationalStatus.Down or OperationalStatus.NotPresent or OperationalStatus.LowerLayerDown => "Disconnected",
                    _ => "Unknown"
                };
            }
            catch
            {
                return "Unknown";
            }
        }
    }

    public bool IsUp => StatusKind == "Connected";

    public string StatusBadge => StatusKind switch
    {
        "Connected" => L("Status.Connected"),
        "Disconnected" => L("Status.Disconnected"),
        _ => L("Status.Unknown")
    };

    public string Ipv4Mask => GetIpv4Info()?.Mask ?? string.Empty;

    public string Dns1 => GetDnsList().ElementAtOrDefault(0) ?? string.Empty;

    public string Dns2 => GetDnsList().ElementAtOrDefault(1) ?? string.Empty;

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

    public string DnsServers => string.Join(", ", GetDnsList());

    private System.Collections.Generic.List<string> GetDnsList()
    {
        try
        {
            var properties = Adapter.GetIPProperties();
            return properties.DnsAddresses
                .Where(d => d.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(d => d.ToString())
                .ToList();
        }
        catch
        {
            return new System.Collections.Generic.List<string>();
        }
    }

    public string DetailLine => $"{TypeName}{Separator}{SpeedText}{Separator}MAC {MacAddress}{Separator}{DeviceName} ({Manufacturer})";

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

    /// <summary>
    /// Reads Name / Manufacturer for all adapters with one WMI query.
    /// A query per adapter takes about 0.2 s each and made startup slow on PCs with many adapters.
    /// </summary>
    public static IReadOnlyDictionary<string, (string Name, string Manufacturer)> LoadDeviceInfoMap()
    {
        var map = new Dictionary<string, (string Name, string Manufacturer)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT GUID, Name, Manufacturer FROM Win32_NetworkAdapter WHERE GUID IS NOT NULL");
            foreach (ManagementObject mo in searcher.Get())
            {
                using (mo)
                {
                    if (mo["GUID"] is string guid && !string.IsNullOrWhiteSpace(guid))
                    {
                        map[guid] = (mo["Name"] as string ?? string.Empty, mo["Manufacturer"] as string ?? string.Empty);
                    }
                }
            }
        }
        catch
        {
        }

        return map;
    }

    /// <summary>
    /// Returns the GUIDs of adapters shown in Windows "Network Connections" (MSFT_NetAdapter, Hidden = False,
    /// excluding Not Present). GetAllNetworkInterfaces also returns filter layers (Npcap / WFP / QoS -0000),
    /// WAN Miniports and vSwitch ports, which must not be offered as candidates.
    /// Returns null when the query fails so that callers can fall back to the unfiltered list.
    /// </summary>
    public static ISet<string>? LoadVisibleAdapterIds()
    {
        const uint NotPresent = 6;
        try
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var searcher = new ManagementObjectSearcher(
                @"root\StandardCimv2",
                "SELECT InterfaceGuid, Hidden, InterfaceOperationalStatus FROM MSFT_NetAdapter");
            foreach (ManagementObject mo in searcher.Get())
            {
                using (mo)
                {
                    if (mo["Hidden"] is true)
                    {
                        continue;
                    }

                    if (mo["InterfaceOperationalStatus"] is uint status && status == NotPresent)
                    {
                        continue;
                    }

                    if (mo["InterfaceGuid"] is string guid && !string.IsNullOrWhiteSpace(guid))
                    {
                        ids.Add(guid);
                    }
                }
            }

            return ids.Count > 0 ? ids : null;
        }
        catch
        {
            return null;
        }
    }

    internal static IEnumerable<NetworkInterface> FilterVisible(
        IEnumerable<NetworkInterface> adapters,
        ISet<string>? visibleIds)
    {
        return visibleIds == null ? adapters : adapters.Where(adapter => visibleIds.Contains(adapter.Id));
    }

    private static (string Name, string Manufacturer) ResolveDeviceInfo(
        NetworkInterface adapter,
        IReadOnlyDictionary<string, (string Name, string Manufacturer)>? deviceInfo)
    {
        if (deviceInfo != null && !string.IsNullOrWhiteSpace(adapter.Id) &&
            deviceInfo.TryGetValue(adapter.Id, out var info))
        {
            return (string.IsNullOrWhiteSpace(info.Name) ? adapter.Description : info.Name,
                string.IsNullOrWhiteSpace(info.Manufacturer) ? L("Text.NotSet") : info.Manufacturer);
        }

        return (adapter.Description, L("Text.NotSet"));
    }
}
