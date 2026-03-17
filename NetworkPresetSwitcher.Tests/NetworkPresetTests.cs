using NetworkPresetSwitcher.Models;
using Xunit;

namespace NetworkPresetSwitcher.Tests;

public class NetworkPresetTests
{
    [Fact]
    public void NetworkPreset_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var preset = new NetworkPreset();

        // Assert
        Assert.Equal(string.Empty, preset.Name);
        Assert.Equal(string.Empty, preset.IP);
        Assert.False(preset.IsDhcp);
        Assert.Equal("Preset.Unnamed", preset.DisplayName);
    }

    [Fact]
    public void NetworkPreset_SettingIpToDhcp_UpdatesIsDhcp()
    {
        // Arrange
        var preset = new NetworkPreset();

        // Act
        preset.IP = "dhcp";

        // Assert
        Assert.True(preset.IsDhcp);
        Assert.Equal("Mode.Dhcp", preset.IpLine);
    }

    [Fact]
    public void NetworkPreset_SettingIsDhcpToTrue_UpdatesIp()
    {
        // Arrange
        var preset = new NetworkPreset { IP = "192.168.1.10" };

        // Act
        preset.IsDhcp = true;

        // Assert
        Assert.Equal("dhcp", preset.IP);
        Assert.True(preset.IsDhcp);
    }

    [Fact]
    public void NetworkPreset_SettingIsDhcpToFalse_RestoresLastStaticIp()
    {
        // Arrange
        var preset = new NetworkPreset { IP = "192.168.1.10" };
        preset.IsDhcp = true; // This should save "192.168.1.10" as last static IP

        // Act
        preset.IsDhcp = false;

        // Assert
        Assert.Equal("192.168.1.10", preset.IP);
        Assert.False(preset.IsDhcp);
    }

    [Fact]
    public void NetworkPreset_Clone_CreatesDeepCopy()
    {
        // Arrange
        var original = new NetworkPreset
        {
            Name = "Original",
            IP = "192.168.1.10",
            Subnet = "255.255.255.0",
            Gateway = "192.168.1.1",
            DNS1 = "8.8.8.8",
            DNS2 = "8.8.4.4",
            Comment = "Test Comment"
        };

        // Act
        var clone = original.Clone();

        // Assert
        Assert.NotSame(original, clone);
        Assert.Equal(original.Name, clone.Name);
        Assert.Equal(original.IP, clone.IP);
        Assert.Equal(original.Subnet, clone.Subnet);
        Assert.Equal(original.Gateway, clone.Gateway);
        Assert.Equal(original.DNS1, clone.DNS1);
        Assert.Equal(original.DNS2, clone.DNS2);
        Assert.Equal(original.Comment, clone.Comment);
    }
}
