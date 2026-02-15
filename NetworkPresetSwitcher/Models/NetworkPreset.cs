using System;
using System.Text.Json.Serialization;
using NetworkPresetSwitcher.Infrastructure;

namespace NetworkPresetSwitcher.Models;

public sealed class NetworkPreset : ObservableObject
{
    private const string DhcpToken = "dhcp";

    private string _name = string.Empty;
    private string _group = string.Empty;
    private string _ip = string.Empty;
    private string _subnet = string.Empty;
    private string _gateway = string.Empty;
    private string _dns1 = string.Empty;
    private string _dns2 = string.Empty;
    private string _comment = string.Empty;
    private string _lastStaticIp = string.Empty;

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public string Group
    {
        get => _group;
        set => SetProperty(ref _group, value);
    }

    public string IP
    {
        get => _ip;
        set
        {
            if (SetProperty(ref _ip, value))
            {
                if (!string.Equals(_ip, DhcpToken, StringComparison.OrdinalIgnoreCase))
                {
                    _lastStaticIp = _ip;
                }

                OnPropertyChanged(nameof(IsDhcp));
                OnPropertyChanged(nameof(IpLine));
            }
        }
    }

    public string Subnet
    {
        get => _subnet;
        set
        {
            if (SetProperty(ref _subnet, value))
            {
            }
        }
    }

    public string Gateway
    {
        get => _gateway;
        set
        {
            if (SetProperty(ref _gateway, value))
            {
            }
        }
    }

    public string DNS1
    {
        get => _dns1;
        set
        {
            if (SetProperty(ref _dns1, value))
            {
            }
        }
    }

    public string DNS2
    {
        get => _dns2;
        set
        {
            if (SetProperty(ref _dns2, value))
            {
            }
        }
    }

    public string Comment
    {
        get => _comment;
        set => SetProperty(ref _comment, value);
    }

    [JsonIgnore]
    public bool IsDhcp
    {
        get => string.Equals(IP, DhcpToken, StringComparison.OrdinalIgnoreCase);
        set
        {
            if (value)
            {
                if (!IsDhcp)
                {
                    _lastStaticIp = IP;
                    IP = DhcpToken;
                }
            }
            else
            {
                if (IsDhcp)
                {
                    IP = string.IsNullOrWhiteSpace(_lastStaticIp) ? string.Empty : _lastStaticIp;
                }
            }

            OnPropertyChanged(nameof(IsDhcp));
            OnPropertyChanged(nameof(IpLine));
        }
    }

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? L("Preset.Unnamed") : Name;

    [JsonIgnore]
    public string IpLine
    {
        get
        {
            if (IsDhcp)
            {
                return L("Mode.Dhcp");
            }

            return string.IsNullOrWhiteSpace(IP) ? L("Preset.Ip.NotSet") : IP;
        }
    }

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(IpLine));
    }

    public NetworkPreset Clone()
    {
        return new NetworkPreset
        {
            Name = Name,
            Group = Group,
            IP = IP,
            Subnet = Subnet,
            Gateway = Gateway,
            DNS1 = DNS1,
            DNS2 = DNS2,
            Comment = Comment
        };
    }

    public override string ToString()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return L("Preset.Unnamed");
        }

        if (IsDhcp)
        {
            return $"{Name} ({L("Mode.Dhcp")})";
        }

        return $"{Name} ({IP})";
    }

    private static string L(string key) => Localization.T(key);
    private static string LF(string key, params object[] args) => Localization.Format(key, args);
}

