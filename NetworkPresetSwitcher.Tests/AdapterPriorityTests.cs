using NetworkPresetSwitcher.ViewModels;
using Xunit;

namespace NetworkPresetSwitcher.Tests;

public class AdapterPriorityTests
{
    [Theory]
    [InlineData("Intel(R) Ethernet Connection")]
    [InlineData("Realtek PCIe GbE Family Controller")]
    [InlineData("Qualcomm FastConnect Wi-Fi")]
    public void IsInternalAdapterName_ReturnsFalseForPhysicalAdapters(string adapterName)
    {
        Assert.False(MainViewModel.IsInternalAdapterName(adapterName));
    }

    [Theory]
    [InlineData("vEthernet (Default Switch)")]
    [InlineData("Hyper-V Virtual Ethernet Adapter")]
    [InlineData("VMware Virtual Ethernet Adapter")]
    [InlineData("VirtualBox Host-Only Ethernet Adapter")]
    [InlineData("WSL")]
    [InlineData("DockerNAT")]
    [InlineData("Npcap Loopback Adapter")]
    [InlineData("TAP-Windows Adapter V9")]
    public void IsInternalAdapterName_ReturnsTrueForInternalOrVirtualAdapters(string adapterName)
    {
        Assert.True(MainViewModel.IsInternalAdapterName(adapterName));
    }
}
