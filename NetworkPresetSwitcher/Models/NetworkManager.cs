using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Text;
using NetworkPresetSwitcher.Infrastructure;

namespace NetworkPresetSwitcher.Models;

public static class NetworkManager
{
    private static string L(string key) => Localization.T(key);
    private static string LF(string key, params object[] args) => Localization.Format(key, args);

    public static void ApplyPreset(NetworkInterface adapter, NetworkPreset preset)
    {
        if (!IsAdministrator())
        {
            var errorMsg = L("Network.Error.AdminRequired");
            throw new Exception(errorMsg);
        }

        if (adapter.OperationalStatus != OperationalStatus.Up)
        {
            if (adapter.OperationalStatus == OperationalStatus.Down)
            {
                try
                {
                    EnableAdapter(adapter);
                    System.Threading.Thread.Sleep(2000);

                    var refreshedAdapter = GetRefreshedAdapter(adapter.Name);
                    if (refreshedAdapter != null && refreshedAdapter.OperationalStatus == OperationalStatus.Up)
                    {
                        adapter = refreshedAdapter;
                    }
                    else
                    {
                        var errorMsg = LF("Network.Error.EnableAdapterFailed", adapter.Name, adapter.OperationalStatus);
                        throw new Exception(errorMsg);
                    }
                }
                catch (Exception enableException)
                {
                    var errorMsg = LF("Network.Error.EnableAdapterFailedWithError", adapter.Name, adapter.OperationalStatus, enableException.Message);
                    throw new Exception(errorMsg);
                }
            }
            else
            {
                var errorMsg = LF("Network.Error.AdapterNotUp", adapter.Name, adapter.OperationalStatus);
                throw new Exception(errorMsg);
            }
        }

        if (preset.IsDhcp)
        {
            try
            {
                ApplyDhcpWithNetsh(adapter);
            }
            catch (Exception netshException)
            {
                try
                {
                    ApplyDhcpWithWmi(adapter);
                }
                catch (Exception wmiException)
                {
                    if (netshException.Message.Contains("DHCP is already enabled", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    var errorMsg = LF("Network.Error.DhcpApplyFailed", adapter.Name, adapter.OperationalStatus, adapter.NetworkInterfaceType, netshException.Message, wmiException.Message);
                    throw new Exception(errorMsg);
                }
            }
        }
        else
        {
            var result = RunNetshCommand($"interface ip set address \"{adapter.Name}\" static {preset.IP} {preset.Subnet} {preset.Gateway}");
            if (result != 0)
            {
                var detailedError = GetDetailedNetshError($"interface ip set address \"{adapter.Name}\" static {preset.IP} {preset.Subnet} {preset.Gateway}");
                var errorMsg = LF("Network.Error.StaticIpApplyFailed", adapter.Name, adapter.OperationalStatus, adapter.NetworkInterfaceType, detailedError);
                throw new Exception(errorMsg);
            }

            if (!string.IsNullOrEmpty(preset.DNS1))
            {
                result = RunNetshCommand($"interface ip set dns \"{adapter.Name}\" static {preset.DNS1}");
                if (result != 0)
                {
                    var detailedError = GetDetailedNetshError($"interface ip set dns \"{adapter.Name}\" static {preset.DNS1}");
                    var errorMsg = LF("Network.Error.Dns1ApplyFailed", adapter.Name, adapter.OperationalStatus, adapter.NetworkInterfaceType, detailedError);
                    throw new Exception(errorMsg);
                }
            }

            if (!string.IsNullOrEmpty(preset.DNS2))
            {
                result = RunNetshCommand($"interface ip add dns \"{adapter.Name}\" {preset.DNS2} index=2");
                if (result != 0)
                {
                    var detailedError = GetDetailedNetshError($"interface ip add dns \"{adapter.Name}\" {preset.DNS2} index=2");
                    var errorMsg = LF("Network.Error.Dns2ApplyFailed", adapter.Name, adapter.OperationalStatus, adapter.NetworkInterfaceType, detailedError);
                    throw new Exception(errorMsg);
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

    private static void EnableAdapter(NetworkInterface adapter)
    {
        var result = RunNetshCommand($"interface set interface \"{adapter.Name}\" admin=enable");
        if (result != 0)
        {
            var detailedError = GetDetailedNetshError($"interface set interface \"{adapter.Name}\" admin=enable");
            throw new Exception(LF("Network.Error.EnableAdapterError", detailedError));
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

    private static void ApplyDhcpWithNetsh(NetworkInterface adapter)
    {
        var result = RunNetshCommand($"interface ip set address \"{adapter.Name}\" dhcp");
        if (result != 0)
        {
            var detailedError = GetDetailedNetshError($"interface ip set address \"{adapter.Name}\" dhcp");
            if (detailedError.Contains("DHCP is already enabled", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            throw new Exception(LF("Network.Error.DhcpApplyFailedNetsh", adapter.Name, adapter.OperationalStatus, adapter.NetworkInterfaceType, detailedError));
        }

        result = RunNetshCommand($"interface ip set dns \"{adapter.Name}\" dhcp");
        if (result != 0)
        {
            var detailedError = GetDetailedNetshError($"interface ip set dns \"{adapter.Name}\" dhcp");
            if (detailedError.Contains("DHCP is already enabled", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            throw new Exception(LF("Network.Error.DnsDhcpApplyFailed", adapter.Name, adapter.OperationalStatus, adapter.NetworkInterfaceType, detailedError));
        }
    }

    private static void ApplyDhcpWithWmi(NetworkInterface adapter)
    {
        try
        {
            var managementAssembly = typeof(ManagementScope).Assembly;
            if (managementAssembly == null)
            {
                throw new Exception(L("Network.Error.SystemManagementMissing"));
            }

            var scope = new ManagementScope("root\\cimv2");
            scope.Connect();

            var query = new SelectQuery($"SELECT * FROM Win32_NetworkAdapterConfiguration WHERE Description = '{adapter.Description}'");
            using (var searcher = new ManagementObjectSearcher(scope, query))
            {
                var configurations = searcher.Get();
                if (configurations.Count == 0)
                {
                    throw new Exception(LF("Network.Error.WmiAdapterNotFound", adapter.Description));
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
                            throw new Exception(LF("Network.Error.WmiDhcpFailed", returnValue));
                        }
                    }
                }
            }
        }
        catch (ManagementException ex)
        {
            throw new Exception(LF("Network.Error.WmiApplyFailed", ex.Message));
        }
        catch (System.Reflection.ReflectionTypeLoadException ex)
        {
            throw new Exception(LF("Network.Error.SystemManagementLoadFailed", ex.Message));
        }
        catch (FileNotFoundException ex)
        {
            throw new Exception(LF("Network.Error.SystemManagementNotFound", ex.Message));
        }
        catch (Exception ex)
        {
            throw new Exception(LF("Network.Error.WmiApplyFailed", ex.Message));
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

    private static int RunNetshCommand(string command)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "netsh",
            Arguments = command,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        try
        {
            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    throw new Exception(L("Network.Error.ProcessStartFailed"));
                }

                process.WaitForExit();

                var error = DecodeOutput(process.StandardError.ReadToEnd());
                var output = DecodeOutput(process.StandardOutput.ReadToEnd());

                if (process.ExitCode != 0)
                {
                    if (output.Contains("DHCP is already enabled", StringComparison.OrdinalIgnoreCase) ||
                        error.Contains("DHCP is already enabled", StringComparison.OrdinalIgnoreCase))
                    {
                        return 0;
                    }

                    throw new Exception(LF("Network.Error.CommandFailed", command, process.ExitCode, error, output));
                }

                return process.ExitCode;
            }
        }
        catch (Exception ex)
        {
            throw new Exception(LF("Network.Error.CommandFailedWithError", command, ex.Message));
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

    private static string GetDetailedNetshError(string command)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = command,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    return L("Network.Error.ProcessStartFailed");
                }

                process.WaitForExit();

                var error = DecodeOutput(process.StandardOutput.ReadToEnd());
                var output = DecodeOutput(process.StandardError.ReadToEnd());

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
}
