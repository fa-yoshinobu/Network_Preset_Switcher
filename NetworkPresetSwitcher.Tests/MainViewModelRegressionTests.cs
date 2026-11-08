using System.Net.NetworkInformation;
using System.Windows;
using NetworkPresetSwitcher.Models;
using NetworkPresetSwitcher.Services;
using NetworkPresetSwitcher.ViewModels;

namespace NetworkPresetSwitcher.Tests;

public class MainViewModelRegressionTests
{
    [Fact]
    public void NewPresetCannotReuseSelectedNameButEditingCanKeepItsName()
    {
        RunOnStaThread(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(directory);
            try
            {
                var dialogs = new TestDialogs();
                var vm = new MainViewModel(dialogs, directory);
                vm.NewPresetCommand.Execute(null);
                vm.EditingPreset!.Name = "Office";
                vm.EditingPreset.IsDhcp = true;
                vm.SavePresetCommand.Execute(null);
                var original = Assert.Single(vm.Presets);
                var path = Path.Combine(directory, "NetworkPresetSwitcher.csv");
                var saved = File.ReadAllText(path);
                vm.NewPresetCommand.Execute(null);
                vm.EditingPreset!.Name = "OFFICE";
                vm.EditingPreset.IsDhcp = true;
                vm.SavePresetCommand.Execute(null);
                Assert.Equal(1, dialogs.Errors);
                Assert.True(vm.IsEditing);
                Assert.Same(original, Assert.Single(vm.Presets));
                Assert.Equal(saved, File.ReadAllText(path));
                vm.CancelEditCommand.Execute(null);
                vm.EditPresetCommand.Execute(null);
                vm.EditingPreset!.Comment = "Updated";
                vm.SavePresetCommand.Execute(null);
                Assert.False(vm.IsEditing);
                Assert.Equal(1, dialogs.Errors);
                Assert.Equal("Updated", original.Comment);
                Assert.Contains("Updated", File.ReadAllText(path));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        });
    }

    [Fact]
    public void SaveFailuresPreserveEditsAndListAndCanBeRetried()
    {
        RunOnStaThread(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(directory);
            try
            {
                var dialogs = new TestDialogs();
                var vm = new MainViewModel(dialogs, directory);
                var path = Path.Combine(directory, "NetworkPresetSwitcher.csv");
                vm.NewPresetCommand.Execute(null);
                vm.EditingPreset!.Name = "Original";
                vm.EditingPreset.IsDhcp = true;
                vm.SavePresetCommand.Execute(null);
                var original = Assert.Single(vm.Presets);
                var saved = File.ReadAllText(path);
                var activityCount = vm.Activity.Count;

                using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    vm.EditPresetCommand.Execute(null);
                    vm.EditingPreset!.Name = "Edited";
                    vm.SavePresetCommand.Execute(null);
                    Assert.True(vm.IsEditing);
                    Assert.Equal("Edited", vm.EditingPreset.Name);
                    Assert.Equal("Original", original.Name);
                    vm.DeletePresetCommand.Execute(null);
                    vm.DuplicatePresetCommand.Execute(null);
                    Assert.Same(original, Assert.Single(vm.Presets));
                    Assert.Same(original, vm.SelectedPreset);
                    vm.NewPresetCommand.Execute(null);
                    vm.EditingPreset!.Name = "New";
                    vm.EditingPreset.IsDhcp = true;
                    vm.SavePresetCommand.Execute(null);
                    Assert.True(vm.IsEditing);
                    Assert.Single(vm.Presets);
                    Assert.Equal(activityCount, vm.Activity.Count);
                    Assert.Equal(4, dialogs.Errors);
                }

                Assert.Equal(saved, File.ReadAllText(path));
                vm.SavePresetCommand.Execute(null);
                Assert.False(vm.IsEditing);
                Assert.Equal(2, vm.Presets.Count);
                Assert.Contains("New", File.ReadAllText(path));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        });
    }

    [Fact]
    public void AdapterRefreshOrdersPhysicalFirstAndPreservesSelection()
    {
        RunOnStaThread(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                var vm = new MainViewModel(new TestDialogs(), directory);
                var virtualAdapter = new TestAdapter("virtual", "vEthernet");
                var ethernet = new TestAdapter("lan", "Intel Ethernet");
                var wifi = new TestAdapter("wifi", "Wi-Fi");
                NetworkInterface[] adapters = { virtualAdapter, ethernet, wifi };
                vm.SelectedAdapter = null;
                vm.RefreshAdapters(adapters);
                Assert.Equal(new[] { "lan", "wifi", "virtual" }, vm.Adapters.Select(a => a.Id));
                Assert.Equal("lan", vm.SelectedAdapter!.Id);
                vm.SelectedAdapter = vm.Adapters.Last();
                vm.RefreshAdapters(adapters);
                Assert.Equal("virtual", vm.SelectedAdapter!.Id);
                vm.RefreshAdapters(new[] { wifi, ethernet });
                Assert.Equal("wifi", vm.SelectedAdapter!.Id);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        });
    }

    [Fact]
    public void EditorBlocksApplyTracksDirtyStateAndStoresPrefixAsMask()
    {
        RunOnStaThread(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(directory);
            try
            {
                var vm = new MainViewModel(new TestDialogs(), directory);
                vm.RefreshAdapters(new NetworkInterface[] { new TestAdapter("lan", "Intel Ethernet") });

                vm.NewPresetCommand.Execute(null);
                Assert.True(vm.IsDirty);
                Assert.False(vm.ApplyCommand.CanExecute(null));
                vm.EditingPreset!.Name = "Line1";
                vm.EditingPreset.IP = "192.168.0.10";
                vm.EditingPreset.Subnet = "/24";
                Assert.True(vm.HasSubnetHint);
                Assert.Equal(0, vm.EditErrorCount);
                vm.SavePresetCommand.Execute(null);

                var saved = Assert.Single(vm.Presets);
                Assert.Equal("255.255.255.0", saved.Subnet);
                Assert.Contains("255.255.255.0", File.ReadAllText(Path.Combine(directory, "NetworkPresetSwitcher.csv")));
                Assert.False(vm.IsEditing);
                Assert.True(vm.ApplyCommand.CanExecute(null));

                vm.EditPresetCommand.Execute(null);
                Assert.False(vm.IsDirty);
                Assert.False(vm.ApplyCommand.CanExecute(null));
                vm.EditingPreset!.IP = "192.168.0.999";
                Assert.True(vm.IsDirty);
                Assert.Equal(1, vm.EditErrorCount);
                Assert.False(vm.SavePresetCommand.CanExecute(null));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        });
    }

    [Fact]
    public void AdapterWithoutIpInformationShowsNoActivePresetAndUnknownStatus()
    {
        RunOnStaThread(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                var vm = new MainViewModel(new TestDialogs(), directory);
                vm.RefreshAdapters(new NetworkInterface[] { new TestAdapter("lan", "Intel Ethernet") });
                var preset = new NetworkPreset { Name = "A", IP = "192.168.0.10" };
                vm.Presets.Add(preset);

                Assert.Equal("Unknown", vm.CurrentStatusKind);
                Assert.False(preset.IsActive);
                Assert.False(MainViewModel.IsPresetActive(preset, vm.SelectedAdapter));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        });
    }

    private static void RunOnStaThread(Action check)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { check(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class TestDialogs : IDialogService
    {
        public int Errors { get; private set; }
        public MessageBoxResult Show(string text, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            if (icon == MessageBoxImage.Error) Errors++;
            return MessageBoxResult.Yes;
        }
        public MessageBoxResult Show(string text, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
            => Show(text, caption, button, icon);
        public bool ConfirmApply(ApplyConfirmation confirmation, out bool skipFutureConfirmation)
        {
            skipFutureConfirmation = false;
            return false;
        }
    }

    [Fact]
    public void FilterVisibleDropsFilterLayersAndKeepsAllWhenIdsUnavailable()
    {
        var lan = new TestAdapter("{AAAA}", "Realtek Ethernet");
        var filterLayer = new TestAdapter("{BBBB}", "Realtek Ethernet-Npcap Packet Driver (NPCAP)-0000");
        NetworkInterface[] adapters = { lan, filterLayer };

        var visible = AdapterViewModel.FilterVisible(adapters, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "{aaaa}" });
        Assert.Equal(new[] { "{AAAA}" }, visible.Select(a => a.Id));

        Assert.Equal(2, AdapterViewModel.FilterVisible(adapters, null).Count());
    }

    private sealed class TestAdapter(string id, string name) : NetworkInterface
    {
        public override string Id => id;
        public override string Name => name;
        public override string Description => name;
        public override NetworkInterfaceType NetworkInterfaceType => NetworkInterfaceType.Ethernet;
        public override IPInterfaceProperties GetIPProperties() => throw new NotSupportedException();
    }
}
