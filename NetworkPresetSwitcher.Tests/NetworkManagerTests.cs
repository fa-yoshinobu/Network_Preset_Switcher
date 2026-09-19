using NetworkPresetSwitcher.Models;
using Xunit;

namespace NetworkPresetSwitcher.Tests;

public class NetworkManagerTests
{
    [Theory]
    [InlineData("{AABBCCDD-1234-5678-90AB-1234567890AB}")]
    [InlineData("aabbccdd-1234-5678-90ab-1234567890ab")]
    public void WmiQueryTargetsOnlyTheAdapterId(string adapterId)
    {
        Assert.Equal(
            "SELECT * FROM Win32_NetworkAdapterConfiguration WHERE SettingID = '{aabbccdd-1234-5678-90ab-1234567890ab}'",
            NetworkManager.CreateAdapterConfigurationQuery(adapterId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ethernet")]
    [InlineData("' OR 1=1")]
    public void WmiQueryRejectsInvalidIds(string adapterId)
    {
        Assert.Throws<FormatException>(() => NetworkManager.CreateAdapterConfigurationQuery(adapterId));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    public void DhcpFallbackResetsDnsAfterEnablingDhcp(uint result)
    {
        var calls = new List<string>();
        NetworkManager.ApplyDhcpConfiguration(method =>
        {
            calls.Add(method);
            return result;
        });
        Assert.Equal(new[] { "EnableDHCP", "SetDNSServerSearchOrder" }, calls);
    }

    [Theory]
    [InlineData("EnableDHCP", 91u)]
    [InlineData("SetDNSServerSearchOrder", 91u)]
    [InlineData("SetDNSServerSearchOrder", null)]
    public void DhcpFallbackDoesNotHideFailures(string failedMethod, uint? result)
    {
        var exception = Assert.Throws<NetworkPresetApplyException>(() =>
            NetworkManager.ApplyDhcpConfiguration(method => method == failedMethod ? result : 0u));
        Assert.Contains(failedMethod, exception.Message);
    }

    [Fact]
    public void NetshCommandUsesArgumentListForAdapterName()
    {
        const string adapterName = "Ethernet \"Lab\" & unsafe";

        var command = NetworkManager.CreateSetStaticAddressCommand(
            adapterName,
            "192.168.10.2",
            "255.255.255.0",
            string.Empty);

        Assert.Equal(
            new[]
            {
                "interface",
                "ip",
                "set",
                "address",
                adapterName,
                "static",
                "192.168.10.2",
                "255.255.255.0"
            },
            command.Arguments);
        Assert.DoesNotContain(string.Empty, command.Arguments);

        var startInfo = command.CreateStartInfo();

        Assert.Equal("netsh", startInfo.FileName);
        Assert.Equal(string.Empty, startInfo.Arguments);
        Assert.Equal(command.Arguments, startInfo.ArgumentList);
        Assert.Contains(adapterName, startInfo.ArgumentList);
        Assert.Contains("\"Ethernet \\\"Lab\\\" & unsafe\"", command.DisplayText, StringComparison.Ordinal);
    }

    [Fact]
    public void NetshCommandIncludesGatewayOnlyWhenProvided()
    {
        var withoutGateway = NetworkManager.CreateSetStaticAddressCommand(
            "Ethernet",
            "192.168.10.2",
            "255.255.255.0",
            string.Empty);
        var withGateway = NetworkManager.CreateSetStaticAddressCommand(
            "Ethernet",
            "192.168.10.2",
            "255.255.255.0",
            "192.168.10.1");

        Assert.DoesNotContain("192.168.10.1", withoutGateway.Arguments);
        Assert.Contains("192.168.10.1", withGateway.Arguments);
    }

    [Fact]
    public void NetworkPresetApplyExceptionPreservesMessageAndInnerException()
    {
        var inner = new InvalidOperationException("inner");

        var exception = new NetworkPresetApplyException("apply failed", inner);

        Assert.Equal("apply failed", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }
}
