using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using NetworkPresetSwitcher.Models;
using NetworkPresetSwitcher.Services;
using NetworkPresetSwitcher.ViewModels;
using Xunit;

namespace NetworkPresetSwitcher.Tests;

public class PresetCsvCharacterizationTests
{
    private static readonly string[] CsvHeader =
    {
        "Type", "Name", "Group", "IP", "Subnet", "Gateway", "DNS1", "DNS2", "Comment", "Language"
    };

    private static readonly string[] RoundTripPreset =
    {
        "Preset",
        "Office, A",
        "Floor 1",
        "192.168.1.10",
        "255.255.255.0",
        "192.168.1.1",
        "8.8.8.8",
        "8.8.4.4",
        "Memo \"quoted\"\nnext",
        string.Empty
    };

    private static readonly string[] Cp932CsvLines =
    {
        "Type,Name,Group,IP,Subnet,Gateway,DNS1,DNS2,Comment,Language",
        "Preset,現場,,192.168.1.20,255.255.255.0,,,,メモ,"
    };

    static PresetCsvCharacterizationTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public void CsvLineRoundTripsQuotedFieldsWithUtf8Bom()
    {
        var path = CreateTempCsvPath();
        try
        {
            var lines = new[]
            {
                PresetCsvFormat.ToCsvLine(CsvHeader),
                PresetCsvFormat.ToCsvLine(RoundTripPreset)
            };
            File.WriteAllText(path, string.Join("\r\n", lines) + "\r\n", new UTF8Encoding(true));

            var rows = PresetCsvFormat.ReadCsvRows(path, out var encodingMode);

            Assert.Equal(CsvEncodingMode.Utf8Bom, encodingMode);
            Assert.Equal("Office, A", rows[1][1]);
            Assert.Equal("Memo \"quoted\"\nnext", rows[1][8]);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public void ReadCsvRowsFallsBackToCp932WhenStrictUtf8Fails()
    {
        var path = CreateTempCsvPath();
        try
        {
            var cp932 = Encoding.GetEncoding(932);
            var contents = string.Join("\r\n", Cp932CsvLines) + "\r\n";
            File.WriteAllBytes(path, cp932.GetBytes(contents));

            var rows = PresetCsvFormat.ReadCsvRows(path, out var encodingMode);

            Assert.Equal(CsvEncodingMode.Cp932Fallback, encodingMode);
            Assert.Equal("現場", rows[1][1]);
            Assert.Equal("メモ", rows[1][8]);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public void TabDelimitedHeaderConvertsTabsOutsideQuotesOnly()
    {
        const string text = "Type\tName\tIP\r\nPreset\t\"Name\tInside\"\t192.168.1.10\r\n";

        var normalized = PresetCsvFormat.NormalizeCsvDelimiters(text);

        Assert.StartsWith("Type,Name,IP", normalized, StringComparison.Ordinal);
        Assert.Contains("\"Name\tInside\"", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain("Preset\t\"", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public void HeaderAliasesMapToCanonicalCsvKeys()
    {
        var header = new[]
        {
            "profile name", "category", "ip address", "mask", "default gateway",
            "primary dns", "secondary dns", "memo", "lang"
        };

        var map = PresetCsvFormat.BuildHeaderMap(header);

        Assert.True(PresetCsvFormat.LooksLikeHeader(header));
        Assert.Equal(0, map["Name"]);
        Assert.Equal(1, map["Group"]);
        Assert.Equal(2, map["IP"]);
        Assert.Equal(3, map["Subnet"]);
        Assert.Equal(4, map["Gateway"]);
        Assert.Equal(5, map["DNS1"]);
        Assert.Equal(6, map["DNS2"]);
        Assert.Equal(7, map["Comment"]);
        Assert.Equal(8, map["Language"]);
        Assert.Equal("ipaddress", PresetCsvFormat.NormalizeHeaderKey("IP Address"));
    }

    [Fact]
    public void LegacyRowsPreserveColumnOrderAndSkipBlankRows()
    {
        var viewModel = CreateUninitializedViewModel();
        var presets = new ObservableCollection<NetworkPreset>();
        SetPresets(viewModel, presets);
        var rows = new[]
        {
            new[] { "Legacy", "10.0.0.5", "255.255.255.0", "10.0.0.1", "1.1.1.1", "1.0.0.1", "Old memo" },
            new[] { string.Empty, string.Empty, string.Empty }
        };

        InvokeParseLegacyRows(viewModel, rows);

        var preset = Assert.Single(presets);
        Assert.Equal("Legacy", preset.Name);
        Assert.Equal(string.Empty, preset.Group);
        Assert.Equal("10.0.0.5", preset.IP);
        Assert.Equal("255.255.255.0", preset.Subnet);
        Assert.Equal("10.0.0.1", preset.Gateway);
        Assert.Equal("1.1.1.1", preset.DNS1);
        Assert.Equal("1.0.0.1", preset.DNS2);
        Assert.Equal("Old memo", preset.Comment);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("255.255.255.0", true)]
    [InlineData("255.255.0.0", true)]
    [InlineData("128.0.0.0", true)]
    [InlineData("0.0.0.0", false)]
    [InlineData("255.255.255.255", false)]
    [InlineData("255.0.255.0", false)]
    [InlineData("255.255.255.1", false)]
    [InlineData("not-an-ip", false)]
    public void SubnetMaskValidationMatchesCurrentBoundaries(string value, bool expected)
    {
        Assert.Equal(expected, Ipv4Validation.IsValidSubnetMask(value));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("192.168.1.10", true)]
    [InlineData("999.168.1.10", false)]
    [InlineData("not-an-ip", false)]
    public void OptionalIpv4ValidationAllowsBlankOnly(string value, bool expected)
    {
        Assert.Equal(expected, Ipv4Validation.IsValidIpv4Optional(value));
    }

    [Fact]
    public void NaturalComparerSortsGroupsThenNumericNameSegments()
    {
        var items = new ArrayList
        {
            new NetworkPreset { Group = "Lab", Name = "Preset 10" },
            new NetworkPreset { Group = "Lab", Name = "Preset 02" },
            new NetworkPreset { Group = "Lab", Name = "Preset 2" },
            new NetworkPreset { Group = "Lab", Name = "Preset 1" },
            new NetworkPreset { Group = "Admin", Name = "Preset 99" }
        };

        items.Sort(new PresetNaturalComparer());

        Assert.Collection(
            items.Cast<NetworkPreset>(),
            preset => Assert.Equal("Preset 99", preset.Name),
            preset => Assert.Equal("Preset 1", preset.Name),
            preset => Assert.Equal("Preset 2", preset.Name),
            preset => Assert.Equal("Preset 02", preset.Name),
            preset => Assert.Equal("Preset 10", preset.Name));
    }

    private static string CreateTempCsvPath()
    {
        return Path.Combine(Path.GetTempPath(), $"NetworkPresetSwitcher.Tests.{Guid.NewGuid():N}.csv");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static MainViewModel CreateUninitializedViewModel()
    {
        return (MainViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MainViewModel));
    }

    private static void SetPresets(MainViewModel viewModel, ObservableCollection<NetworkPreset> presets)
    {
        var field = typeof(MainViewModel).GetField("<Presets>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(viewModel, presets);
    }

    private static void InvokeParseLegacyRows(MainViewModel viewModel, IEnumerable<string[]> rows)
    {
        var method = typeof(MainViewModel).GetMethod("ParseLegacyRows", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(viewModel, new object[] { rows });
    }
}
