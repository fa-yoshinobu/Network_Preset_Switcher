using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using NetworkPresetSwitcher.Infrastructure;

namespace NetworkPresetSwitcher.Models;

public static class NetworkManager
{
    private static readonly TimeSpan AdapterEnableDelay = TimeSpan.FromSeconds(2);

    private static string L(string key) => Localization.T(key);
    private static string LF(string key, params object[] args) => Localization.Format(key, args);

    public static void ApplyPreset(NetworkInterface adapter, NetworkPreset preset)
    {
        ApplyPresetAsync(adapter, preset).GetAwaiter().GetResult();
    }

    public static async Task ApplyPresetAsync(NetworkInterface adapter, NetworkPreset preset)
    {
        if (!IsAdministrator())
        {
            var errorMsg = L("Network.Error.AdminRequired");
            throw CreateApplyException(errorMsg);
        }

        if (adapter.OperationalStatus != OperationalStatus.Up)
        {
            if (adapter.OperationalStatus == OperationalStatus.Down)
            {
                try
                {
                    await EnableAdapterAsync(adapter).ConfigureAwait(false);
                    await Task.Delay(AdapterEnableDelay).ConfigureAwait(false);

                    var refreshedAdapter = GetRefreshedAdapter(adapter.Name);
                    if (refreshedAdapter != null && refreshedAdapter.OperationalStatus == OperationalStatus.Up)
                    {
                        adapter = refreshedAdapter;
                    }
                    else
                    {
                        var errorMsg = LF("Network.Error.EnableAdapterFailed", adapter.Name, adapter.OperationalStatus);
                        throw CreateApplyException(errorMsg);
                    }
                }
                catch (Exception enableException)
                {
                    var errorMsg = LF("Network.Error.EnableAdapterFailedWithError", adapter.Name, adapter.OperationalStatus, enableException.Message);
                    throw CreateApplyException(errorMsg, enableException);
                }
            }
            else
            {
                var errorMsg = LF("Network.Error.AdapterNotUp", adapter.Name, adapter.OperationalStatus);
                throw CreateApplyException(errorMsg);
            }
        }

        if (preset.IsDhcp)
        {
            try
            {
                await ApplyDhcpWithNetshAsync(adapter).ConfigureAwait(false);
            }
            catch (Exception netshException)
            {
                try
                {
                    await Task.Run(() => ApplyDhcpWithWmi(adapter)).ConfigureAwait(false);
                }
                catch (Exception wmiException)
                {
                    if (netshException.Message.Contains("DHCP is already enabled", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    var errorMsg = LF("Network.Error.DhcpApplyFailed", adapter.Name, adapter.OperationalStatus, adapter.NetworkInterfaceType, netshException.Message, wmiException.Message);
                    throw CreateApplyException(errorMsg, wmiException);
                }
            }
        }
        else
        {
            var result = await RunNetshCommandAsync(CreateSetStaticAddressCommand(adapter.Name, preset.IP, preset.Subnet, preset.Gateway)).ConfigureAwait(false);
            if (result != 0)
            {
                var detailedError = await GetDetailedNetshErrorAsync(CreateSetStaticAddressCommand(adapter.Name, preset.IP, preset.Subnet, preset.Gateway)).ConfigureAwait(false);
                var errorMsg = LF("Network.Error.StaticIpApplyFailed", adapter.Name, adapter.OperationalStatus, adapter.NetworkInterfaceType, detailedError);
                throw CreateApplyException(errorMsg);
            }

            if (!string.IsNullOrEmpty(preset.DNS1))
            {
                result = await RunNetshCommandAsync(CreateSetStaticDnsCommand(adapter.Name, preset.DNS1)).ConfigureAwait(false);
                if (result != 0)
                {
                    var detailedError = await GetDetailedNetshErrorAsync(CreateSetStaticDnsCommand(adapter.Name, preset.DNS1)).ConfigureAwait(false);
                    var errorMsg = LF("Network.Error.Dns1ApplyFailed", adapter.Name, adapter.OperationalStatus, adapter.NetworkInterfaceType, detailedError);
                    throw CreateApplyException(errorMsg);
                }
            }

            if (!string.IsNullOrEmpty(preset.DNS2))
            {
                result = await RunNetshCommandAsync(CreateAddDnsCommand(adapter.Name, preset.DNS2)).ConfigureAwait(false);
                if (result != 0)
                {
                    var detailedError = await GetDetailedNetshErrorAsync(CreateAddDnsCommand(adapter.Name, preset.DNS2)).ConfigureAwait(false);
                    var errorMsg = LF("Network.Error.Dns2ApplyFailed", adapter.Name, adapter.OperationalStatus, adapter.NetworkInterfaceType, detailedError);
                    throw CreateApplyException(errorMsg);
                }
            }
        }
    }

    public static string GetAdapterDetailedInfo(NetworkInterface adapter)
    {
        var info = new StringBuilder();
        info.AppendLine(LF("Network.Info.AdapterName", adapter.Name));
        info.AppendLine(LF("Network.Info.Description", adapter.Description));
        info.AppendLine(LF("Network.Info.Status", adapter.OperationalStatus));
        info.AppendLine(LF("Network.Info.Type", adapter.NetworkInterfaceType));
        info.AppendLine(LF("Network.Info.Speed", GetSpeedString(adapter.Speed)));
        info.AppendLine(LF("Network.Info.Mac", GetMacAddress(adapter)));

        if (adapter.OperationalStatus == OperationalStatus.Down)
        {
            info.AppendLine(L("Network.Info.PhysicalIssueHeader"));
            info.AppendLine(L("Network.Info.PhysicalIssue1"));
            info.AppendLine(L("Network.Info.PhysicalIssue2"));
            info.AppendLine(L("Network.Info.PhysicalIssue3"));
        }

        return info.ToString();
    }

    internal static NetshCommand CreateSetInterfaceEnabledCommand(string adapterName)
    {
        return new NetshCommand("interface", "set", "interface", adapterName, "admin=enable");
    }

    internal static NetshCommand CreateSetStaticAddressCommand(string adapterName, string ip, string subnet, string gateway)
    {
        var arguments = new List<string>
        {
            "interface",
            "ip",
            "set",
            "address",
            adapterName,
            "static",
            ip,
            subnet
        };

        if (!string.IsNullOrWhiteSpace(gateway))
        {
            arguments.Add(gateway);
        }

        return new NetshCommand(arguments);
    }

    internal static NetshCommand CreateSetDhcpAddressCommand(string adapterName)
    {
        return new NetshCommand("interface", "ip", "set", "address", adapterName, "dhcp");
    }

    internal static NetshCommand CreateSetStaticDnsCommand(string adapterName, string dns)
    {
        return new NetshCommand("interface", "ip", "set", "dns", adapterName, "static", dns);
    }

    internal static NetshCommand CreateSetDhcpDnsCommand(string adapterName)
    {
        return new NetshCommand("interface", "ip", "set", "dns", adapterName, "dhcp");
    }

    internal static NetshCommand CreateAddDnsCommand(string adapterName, string dns)
    {
        return new NetshCommand("interface", "ip", "add", "dns", adapterName, dns, "index=2");
    }

    private static async Task EnableAdapterAsync(NetworkInterface adapter)
    {
        var command = CreateSetInterfaceEnabledCommand(adapter.Name);
        var result = await RunNetshCommandAsync(command).ConfigureAwait(false);
        if (result != 0)
        {
            var detailedError = await GetDetailedNetshErrorAsync(command).ConfigureAwait(false);
            throw CreateApplyException(LF("Network.Error.EnableAdapterError", detailedError));
        }
    }

    private static NetworkInterface? GetRefreshedAdapter(string adapterName)
    {
        try
        {
            var adapters = NetworkInterface.GetAllNetworkInterfaces();
            return adapters.FirstOrDefault(a => a.Name == adapterName);
        }
        catch
        {
            return null;
        }
    }

    private static string GetSpeedString(long speed)
    {
        if (speed == -1) return L("Text.NotSet");
        if (speed >= 1000000000) return $"{speed / 1000000000} Gbps";
        if (speed >= 1000000) return $"{speed / 1000000} Mbps";
        if (speed >= 1000) return $"{speed / 1000} Kbps";
        return $"{speed} bps";
    }

    private static string GetMacAddress(NetworkInterface adapter)
    {
        try
        {
            return BitConverter.ToString(adapter.GetPhysicalAddress().GetAddressBytes()).Replace("-", ":");
        }
        catch
        {
            return L("Network.Error.MacNotAvailable");
        }
    }

    private static async Task ApplyDhcpWithNetshAsync(NetworkInterface adapter)
    {
        var addressCommand = CreateSetDhcpAddressCommand(adapter.Name);
        var result = await RunNetshCommandAsync(addressCommand).ConfigureAwait(false);
        if (result != 0)
        {
            var detailedError = await GetDetailedNetshErrorAsync(addressCommand).ConfigureAwait(false);
            if (detailedError.Contains("DHCP is already enabled", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            throw CreateApplyException(LF("Network.Error.DhcpApplyFailedNetsh", adapter.Name, adapter.OperationalStatus, adapter.NetworkInterfaceType, detailedError));
        }

        var dnsCommand = CreateSetDhcpDnsCommand(adapter.Name);
        result = await RunNetshCommandAsync(dnsCommand).ConfigureAwait(false);
        if (result != 0)
        {
            var detailedError = await GetDetailedNetshErrorAsync(dnsCommand).ConfigureAwait(false);
            if (detailedError.Contains("DHCP is already enabled", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            throw CreateApplyException(LF("Network.Error.DnsDhcpApplyFailed", adapter.Name, adapter.OperationalStatus, adapter.NetworkInterfaceType, detailedError));
        }
    }

    private static void ApplyDhcpWithWmi(NetworkInterface adapter)
    {
        try
        {
            var managementAssembly = typeof(ManagementScope).Assembly;
            if (managementAssembly == null)
            {
                throw CreateApplyException(L("Network.Error.SystemManagementMissing"));
            }

            var scope = new ManagementScope("root\\cimv2");
            scope.Connect();

            var query = new SelectQuery($"SELECT * FROM Win32_NetworkAdapterConfiguration WHERE Description = '{adapter.Description}'");
            using (var searcher = new ManagementObjectSearcher(scope, query))
            {
                var configurations = searcher.Get();
                if (configurations.Count == 0)
                {
                    throw CreateApplyException(LF("Network.Error.WmiAdapterNotFound", adapter.Description));
                }

                foreach (ManagementObject configObj in configurations)
                {
                    var inParams = configObj.GetMethodParameters("EnableDHCP");
                    var outParams = configObj.InvokeMethod("EnableDHCP", inParams, null);

                    if (outParams != null && outParams["ReturnValue"] != null)
                    {
                        var returnValue = Convert.ToInt32(outParams["ReturnValue"]);
                        if (returnValue != 0)
                        {
                            throw CreateApplyException(LF("Network.Error.WmiDhcpFailed", returnValue));
                        }
                    }
                }
            }
        }
        catch (ManagementException ex)
        {
            throw CreateApplyException(LF("Network.Error.WmiApplyFailed", ex.Message), ex);
        }
        catch (System.Reflection.ReflectionTypeLoadException ex)
        {
            throw CreateApplyException(LF("Network.Error.SystemManagementLoadFailed", ex.Message), ex);
        }
        catch (FileNotFoundException ex)
        {
            throw CreateApplyException(LF("Network.Error.SystemManagementNotFound", ex.Message), ex);
        }
        catch (Exception ex)
        {
            throw CreateApplyException(LF("Network.Error.WmiApplyFailed", ex.Message), ex);
        }
    }

    private static bool IsAdministrator()
    {
        using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
        {
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
    }

    private static async Task<int> RunNetshCommandAsync(NetshCommand command)
    {
        try
        {
            using (var process = Process.Start(command.CreateStartInfo()))
            {
                if (process == null)
                {
                    throw CreateApplyException(L("Network.Error.ProcessStartFailed"));
                }

                var errorTask = process.StandardError.ReadToEndAsync();
                var outputTask = process.StandardOutput.ReadToEndAsync();

                await process.WaitForExitAsync().ConfigureAwait(false);

                var error = DecodeOutput(await errorTask.ConfigureAwait(false));
                var output = DecodeOutput(await outputTask.ConfigureAwait(false));

                if (process.ExitCode != 0)
                {
                    if (output.Contains("DHCP is already enabled", StringComparison.OrdinalIgnoreCase) ||
                        error.Contains("DHCP is already enabled", StringComparison.OrdinalIgnoreCase))
                    {
                        return 0;
                    }

                    throw CreateApplyException(LF("Network.Error.CommandFailed", command.DisplayText, process.ExitCode, error, output));
                }

                return process.ExitCode;
            }
        }
        catch (Exception ex)
        {
            throw CreateApplyException(LF("Network.Error.CommandFailedWithError", command.DisplayText, ex.Message), ex);
        }
    }

    private static string DecodeOutput(string output)
    {
        try
        {
            if (string.IsNullOrEmpty(output))
            {
                return output;
            }

            byte[] originalBytes;
            try
            {
                originalBytes = Encoding.Default.GetBytes(output);
            }
            catch
            {
                return output;
            }

            var encodings = new[]
            {
                Encoding.GetEncoding("Shift_JIS"),
                Encoding.UTF8,
                Encoding.GetEncoding("CP932"),
                Encoding.GetEncoding("GBK"),
                Encoding.GetEncoding("Big5")
            };

            foreach (var encoding in encodings)
            {
                try
                {
                    var decoded = encoding.GetString(originalBytes);
                    if (!string.IsNullOrEmpty(decoded) && decoded.Any(c => !char.IsControl(c) || c == '\n' || c == '\r' || c == '\t'))
                    {
                        return decoded;
                    }
                }
                catch
                {
                    continue;
                }
            }

            return output;
        }
        catch
        {
            return output;
        }
    }

    private static async Task<string> GetDetailedNetshErrorAsync(NetshCommand command)
    {
        try
        {
            using (var process = Process.Start(command.CreateStartInfo()))
            {
                if (process == null)
                {
                    return L("Network.Error.ProcessStartFailed");
                }

                var errorTask = process.StandardOutput.ReadToEndAsync();
                var outputTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync().ConfigureAwait(false);

                var error = DecodeOutput(await errorTask.ConfigureAwait(false));
                var output = DecodeOutput(await outputTask.ConfigureAwait(false));

                var result = LF("Network.Error.ExitCodeDetail", process.ExitCode);
                if (!string.IsNullOrEmpty(error))
                {
                    result += LF("Network.Error.StdErrorDetail", error);
                }
                if (!string.IsNullOrEmpty(output))
                {
                    result += LF("Network.Error.StdOutDetail", output);
                }

                return result;
            }
        }
        catch (Exception ex)
        {
            return LF("Network.Error.ErrorInfoFailed", ex.Message);
        }
    }

    private static NetworkPresetApplyException CreateApplyException(string message, Exception? innerException = null)
    {
        return innerException == null
            ? new NetworkPresetApplyException(message)
            : new NetworkPresetApplyException(message, innerException);
    }
}

internal sealed class NetshCommand
{
    private readonly string[] _arguments;

    internal NetshCommand(params string[] arguments)
        : this((IEnumerable<string>)arguments)
    {
    }

    internal NetshCommand(IEnumerable<string> arguments)
    {
        _arguments = arguments
            .Where(argument => !string.IsNullOrWhiteSpace(argument))
            .ToArray();
    }

    internal IReadOnlyList<string> Arguments => _arguments;

    internal string DisplayText => string.Join(" ", _arguments.Select(QuoteForDisplay));

    internal ProcessStartInfo CreateStartInfo()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "netsh",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in _arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static string QuoteForDisplay(string value)
    {
        if (value.Length == 0)
        {
            return "\"\"";
        }

        return value.Any(char.IsWhiteSpace) || value.Contains('"', StringComparison.Ordinal)
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;
    }
}
