using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using AppLocalization = NetworkPresetSwitcher.Infrastructure.Localization;
using NetworkPresetSwitcher.Converters;
using NetworkPresetSwitcher.Infrastructure;
using NetworkPresetSwitcher.Models;

namespace NetworkPresetSwitcher.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private const string PresetCsvFileName = "NetworkPresetSwitcher.csv";
    private const int ActivityMaxItems = 500;
    private const string ActivityLogFileName = "NetworkPresetSwitcher.log";
    private const string PresetTypePreset = "Preset";
    private const string PresetTypeSettings = "Settings";

    private static readonly string[] CsvHeader =
    {
        "Type",
        "Name",
        "Group",
        "IP",
        "Subnet",
        "Gateway",
        "DNS1",
        "DNS2",
        "Comment",
        "Language"
    };

    private static readonly Dictionary<string, int> DefaultCsvMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Type"] = 0,
        ["Name"] = 1,
        ["Group"] = 2,
        ["IP"] = 3,
        ["Subnet"] = 4,
        ["Gateway"] = 5,
        ["DNS1"] = 6,
        ["DNS2"] = 7,
        ["Comment"] = 8,
        ["Language"] = 9
    };

    private readonly string _primaryPresetsFilePath;
    private readonly string _activityLogPath;
    private string _activePresetsFilePath = string.Empty;
    private string _presetsStoragePath = string.Empty;
    private bool _warnedLanguageColumnMissing;
    private bool _warnedEncodingFallback;
    private bool _warnedHeaderMissing;
    private bool _warnedSettingsRowMissing;

    private AdapterViewModel? _selectedAdapter;
    private NetworkPreset? _selectedPreset;
    private NetworkPreset? _editingPreset;
    private string _searchText = string.Empty;
    private string _pingTarget = string.Empty;
    private string? _lastAdapterId;
    private string _lastAdapterIp = string.Empty;
    private bool _pingTargetDirty;
    private bool _suppressPingTargetDirty;
    private bool _isNewPreset;
    private bool _isEditing;
    private bool _isPinging;
    private bool _isErrorVisible;
    private string _errorTitle = string.Empty;
    private string _errorDetail = string.Empty;
    private string _errorCause = string.Empty;
    private string _errorFix = string.Empty;

    public MainViewModel()
    {
        var appDirectory = GetAppDirectory();
        _primaryPresetsFilePath = Path.Combine(appDirectory, PresetCsvFileName);
        _activityLogPath = Path.Combine(appDirectory, ActivityLogFileName);
        SetActivePresetsPath(_primaryPresetsFilePath);

        Adapters = new ObservableCollection<AdapterViewModel>();
        Presets = new ObservableCollection<NetworkPreset>();
        Presets.CollectionChanged += OnPresetsChanged;
        Activity = new ObservableCollection<ActivityItem>();

        PresetsView = CollectionViewSource.GetDefaultView(Presets);
        if (PresetsView is ListCollectionView listView)
        {
            listView.CustomSort = new PresetNaturalComparer();
            listView.GroupDescriptions.Clear();
            listView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(NetworkPreset.Group), new GroupNameConverter()));
        }
        else
        {
            PresetsView.SortDescriptions.Add(new SortDescription(nameof(NetworkPreset.Name), ListSortDirection.Ascending));
        }
        PresetsView.Filter = FilterPreset;

        RefreshAdaptersCommand = new RelayCommand(_ => RefreshAdapters());
        ApplyCommand = new RelayCommand(_ => ApplyPreset(), _ => CanApply);
        NewPresetCommand = new RelayCommand(_ => BeginNewPreset());
        EditPresetCommand = new RelayCommand(_ => BeginEditPreset(), _ => SelectedPreset != null && !IsEditing);
        SavePresetCommand = new RelayCommand(_ => SaveEditingPreset(),
            _ => IsEditing && EditingPreset != null &&
                 !IsIpInvalid && !IsSubnetInvalid && !IsGatewayInvalid && !IsDns1Invalid && !IsDns2Invalid);
        CancelEditCommand = new RelayCommand(_ => CancelEditing(), _ => IsEditing);
        DeletePresetCommand = new RelayCommand(_ => DeleteSelectedPreset(), _ => SelectedPreset != null);
        DuplicatePresetCommand = new RelayCommand(_ => DuplicatePreset(), _ => SelectedPreset != null);
        ClearActivityCommand = new RelayCommand(_ => Activity.Clear());
        PingCommand = new RelayCommand(async _ => await PingTargetAsync(),
            _ => !IsPinging && !string.IsNullOrWhiteSpace(PingTarget));
        OpenPresetsFolderCommand = new RelayCommand(_ => OpenPresetsFolder());
        OpenNetworkConnectionsCommand = new RelayCommand(_ => OpenNetworkConnections());
        ShowVersionCommand = new RelayCommand(_ => ShowVersionInfo());
        ToggleLanguageCommand = new RelayCommand(_ => ToggleLanguage());

        AppLocalization.LanguageChanged += OnLanguageChanged;

        LoadPresets();
        RefreshAdapters();

        if (Adapters.Count > 0)
        {
            SelectedAdapter = Adapters[0];
        }

        if (Presets.Count > 0)
        {
            SelectedPreset = Presets[0];
        }

        AddActivity(new ActivityItem(L("Msg.AppReadyTitle"), L("Msg.AppReadyDetail")));
    }

    public ObservableCollection<AdapterViewModel> Adapters { get; }

    public ObservableCollection<NetworkPreset> Presets { get; }

    public ICollectionView PresetsView { get; }

    public ObservableCollection<ActivityItem> Activity { get; }

    public string PresetsStoragePath
    {
        get => _presetsStoragePath;
        private set => SetProperty(ref _presetsStoragePath, value);
    }

    public AdapterViewModel? SelectedAdapter
    {
        get => _selectedAdapter;
        set
        {
            if (SetProperty(ref _selectedAdapter, value))
            {
                OnPropertyChanged(nameof(CurrentStatusBadge));
                OnPropertyChanged(nameof(CurrentModeBadge));
                OnPropertyChanged(nameof(CurrentIpBadge));
                OnPropertyChanged(nameof(CurrentGatewayBadge));
                OnPropertyChanged(nameof(CurrentDnsBadge));
                SyncPingTargetFromAdapter();
                RaiseCommandStates();
            }
        }
    }

    public NetworkPreset? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value))
            {
                BeginEditFromSelected();
                RaiseCommandStates();
            }
        }
    }

    public NetworkPreset? EditingPreset
    {
        get => _editingPreset;
        private set
        {
            var previous = _editingPreset;
            if (SetProperty(ref _editingPreset, value))
            {
                if (previous != null)
                {
                    previous.PropertyChanged -= OnEditingPresetPropertyChanged;
                }
                if (_editingPreset != null)
                {
                    _editingPreset.PropertyChanged += OnEditingPresetPropertyChanged;
                }

                OnPropertyChanged(nameof(IsIpInvalid));
                OnPropertyChanged(nameof(IsSubnetInvalid));
                OnPropertyChanged(nameof(IsGatewayInvalid));
                OnPropertyChanged(nameof(IsDns1Invalid));
                OnPropertyChanged(nameof(IsDns2Invalid));
                RaiseCommandStates();
            }
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (SetProperty(ref _isEditing, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public bool IsIpInvalid =>
        IsEditing &&
        EditingPreset != null &&
        !EditingPreset.IsDhcp &&
        !IsValidIpv4(EditingPreset.IP);

    public bool IsSubnetInvalid =>
        IsEditing &&
        EditingPreset != null &&
        !EditingPreset.IsDhcp &&
        !IsValidSubnetMask(EditingPreset.Subnet);

    public bool IsGatewayInvalid =>
        IsEditing &&
        EditingPreset != null &&
        !EditingPreset.IsDhcp &&
        !IsValidIpv4Optional(EditingPreset.Gateway);

    public bool IsDns1Invalid =>
        IsEditing &&
        EditingPreset != null &&
        !EditingPreset.IsDhcp &&
        !IsValidIpv4Optional(EditingPreset.DNS1);

    public bool IsDns2Invalid =>
        IsEditing &&
        EditingPreset != null &&
        !EditingPreset.IsDhcp &&
        !IsValidIpv4Optional(EditingPreset.DNS2);

    public bool IsErrorVisible
    {
        get => _isErrorVisible;
        private set => SetProperty(ref _isErrorVisible, value);
    }

    public string ErrorTitle
    {
        get => _errorTitle;
        private set => SetProperty(ref _errorTitle, value);
    }

    public string ErrorDetail
    {
        get => _errorDetail;
        private set => SetProperty(ref _errorDetail, value);
    }

    public string ErrorCause
    {
        get => _errorCause;
        private set
        {
            if (SetProperty(ref _errorCause, value))
            {
                OnPropertyChanged(nameof(HasErrorCause));
            }
        }
    }

    public string ErrorFix
    {
        get => _errorFix;
        private set
        {
            if (SetProperty(ref _errorFix, value))
            {
                OnPropertyChanged(nameof(HasErrorFix));
            }
        }
    }

    public bool HasErrorCause => !string.IsNullOrWhiteSpace(ErrorCause);
    public bool HasErrorFix => !string.IsNullOrWhiteSpace(ErrorFix);

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                PresetsView.Refresh();
            }
        }
    }

    public string PingTarget
    {
        get => _pingTarget;
        set
        {
            if (SetProperty(ref _pingTarget, value))
            {
                if (!_suppressPingTargetDirty)
                {
                    _pingTargetDirty = true;
                }
                RaiseCommandStates();
            }
        }
    }

    public bool IsPinging
    {
        get => _isPinging;
        private set
        {
            if (SetProperty(ref _isPinging, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string CurrentStatusBadge => SelectedAdapter?.StatusBadge ?? L("Text.NotSet");
    public string CurrentModeBadge => SelectedAdapter?.ModeBadge ?? L("Text.NotSet");
    public string CurrentIpBadge => SelectedAdapter?.IpBadge ?? L("Label.Ipv4NotSet");
    public string CurrentGatewayBadge => SelectedAdapter?.GatewayBadge ?? L("Label.GwNotSet");
    public string CurrentDnsBadge => SelectedAdapter?.DnsBadge ?? L("Label.DnsNotSet");

    public bool CanApply => SelectedAdapter != null && SelectedPreset != null;

    public RelayCommand RefreshAdaptersCommand { get; }
    public RelayCommand ApplyCommand { get; }
    public RelayCommand NewPresetCommand { get; }
    public RelayCommand EditPresetCommand { get; }
    public RelayCommand SavePresetCommand { get; }
    public RelayCommand CancelEditCommand { get; }
    public RelayCommand DeletePresetCommand { get; }
    public RelayCommand DuplicatePresetCommand { get; }
    public RelayCommand ClearActivityCommand { get; }
    public RelayCommand PingCommand { get; }
    public RelayCommand OpenPresetsFolderCommand { get; }
    public RelayCommand OpenNetworkConnectionsCommand { get; }
    public RelayCommand ShowVersionCommand { get; }
    public RelayCommand ToggleLanguageCommand { get; }

    private static string L(string key) => AppLocalization.T(key);
    private static string LF(string key, params object[] args) => AppLocalization.Format(key, args);

    private bool FilterPreset(object obj)
    {
        if (obj is not NetworkPreset preset)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        var keyword = SearchText.Trim();
            return preset.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || preset.Group.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || preset.IP.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || preset.Comment.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshAdapters()
    {
        var selectedId = SelectedAdapter?.Id;
        Adapters.Clear();

        var adapters = NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter =>
                (adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                 adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) &&
                !adapter.Description.Contains("bluetooth", StringComparison.OrdinalIgnoreCase))
            .Select(adapter => new AdapterViewModel(adapter))
            .ToList();

        foreach (var adapter in adapters)
        {
            Adapters.Add(adapter);
        }

        if (!string.IsNullOrEmpty(selectedId))
        {
            SelectedAdapter = Adapters.FirstOrDefault(a => a.Id == selectedId) ?? Adapters.FirstOrDefault();
        }
        else
        {
            SelectedAdapter = Adapters.FirstOrDefault();
        }

        AddActivity(new ActivityItem(L("Msg.AdaptersRefreshedTitle"), LF("Msg.AdaptersRefreshedDetail", Adapters.Count)));
        ClearError();
    }

    private void OnPresetsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.OfType<NetworkPreset>())
            {
                item.PropertyChanged -= OnPresetPropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<NetworkPreset>())
            {
                item.PropertyChanged += OnPresetPropertyChanged;
            }
        }
    }

    private void OnPresetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NetworkPreset.Group))
        {
            PresetsView.Refresh();
        }
    }

    private void OnEditingPresetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NetworkPreset.IP))
        {
            OnPropertyChanged(nameof(IsIpInvalid));
            RaiseCommandStates();
        }
        else if (e.PropertyName == nameof(NetworkPreset.Subnet))
        {
            OnPropertyChanged(nameof(IsSubnetInvalid));
            RaiseCommandStates();
        }
        else if (e.PropertyName == nameof(NetworkPreset.Gateway))
        {
            OnPropertyChanged(nameof(IsGatewayInvalid));
            RaiseCommandStates();
        }
        else if (e.PropertyName == nameof(NetworkPreset.DNS1))
        {
            OnPropertyChanged(nameof(IsDns1Invalid));
            RaiseCommandStates();
        }
        else if (e.PropertyName == nameof(NetworkPreset.DNS2))
        {
            OnPropertyChanged(nameof(IsDns2Invalid));
            RaiseCommandStates();
        }
        else if (e.PropertyName == nameof(NetworkPreset.IsDhcp))
        {
            OnPropertyChanged(nameof(IsIpInvalid));
            OnPropertyChanged(nameof(IsSubnetInvalid));
            OnPropertyChanged(nameof(IsGatewayInvalid));
            OnPropertyChanged(nameof(IsDns1Invalid));
            OnPropertyChanged(nameof(IsDns2Invalid));
            RaiseCommandStates();
        }
    }

    private void BeginNewPreset()
    {
        EditingPreset = new NetworkPreset();
        _isNewPreset = true;
        IsEditing = true;
        ClearError();
    }

    private void BeginEditPreset()
    {
        if (SelectedPreset == null)
        {
            return;
        }

        EditingPreset = SelectedPreset.Clone();
        _isNewPreset = false;
        IsEditing = true;
        ClearError();
    }

    private void BeginEditFromSelected()
    {
        if (SelectedPreset == null)
        {
            EditingPreset = null;
            _isNewPreset = false;
            IsEditing = false;
            return;
        }

        EditingPreset = SelectedPreset.Clone();
        _isNewPreset = false;
        IsEditing = false;
        ClearError();
    }

    private void CancelEditing()
    {
        if (SelectedPreset != null)
        {
            EditingPreset = SelectedPreset.Clone();
            _isNewPreset = false;
            IsEditing = false;
            return;
        }

        EditingPreset = null;
        _isNewPreset = false;
        IsEditing = false;
    }

    private void SaveEditingPreset()
    {
        if (EditingPreset == null)
        {
            return;
        }

        if (!EditingPreset.IsDhcp && string.IsNullOrWhiteSpace(EditingPreset.Subnet))
        {
            EditingPreset.Subnet = "255.255.255.0";
        }

        if (!ValidatePreset(EditingPreset))
        {
            return;
        }

        var duplicate = Presets.Any(p =>
            !ReferenceEquals(p, SelectedPreset) &&
            p.Name.Equals(EditingPreset.Name, StringComparison.OrdinalIgnoreCase));

        if (duplicate)
        {
            MessageBox.Show(L("Msg.ErrorDuplicatePreset"), L("Msg.ErrorTitle"),
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (_isNewPreset || SelectedPreset == null)
        {
            var newPreset = EditingPreset.Clone();
            Presets.Add(newPreset);
            SelectedPreset = newPreset;
        }
        else
        {
            SelectedPreset.Name = EditingPreset.Name;
            SelectedPreset.Group = EditingPreset.Group;
            SelectedPreset.IP = EditingPreset.IP;
            SelectedPreset.Subnet = EditingPreset.Subnet;
            SelectedPreset.Gateway = EditingPreset.Gateway;
            SelectedPreset.DNS1 = EditingPreset.DNS1;
            SelectedPreset.DNS2 = EditingPreset.DNS2;
            SelectedPreset.Comment = EditingPreset.Comment;
        }

        SavePresets();
        PresetsView.Refresh();
        BeginEditFromSelected();

        AddActivity(new ActivityItem(L("Activity.PresetSavedTitle"), LF("Msg.PresetSaved", SelectedPreset?.Name ?? string.Empty), ActivityLevel.Success));
        ClearError();
    }

    private void DeleteSelectedPreset()
    {
        if (SelectedPreset == null)
        {
            return;
        }

        var result = MessageBox.Show(L("Msg.ConfirmDelete"), L("Msg.ConfirmTitle"),
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        var removedName = SelectedPreset.Name;
        Presets.Remove(SelectedPreset);
        SavePresets();
        PresetsView.Refresh();

        SelectedPreset = Presets.FirstOrDefault();
        AddActivity(new ActivityItem(L("Activity.PresetDeletedTitle"), LF("Msg.PresetDeleted", removedName), ActivityLevel.Warning));
        ClearError();
    }

    private void DuplicatePreset()
    {
        if (SelectedPreset == null)
        {
            return;
        }

        var baseName = SelectedPreset.Name;
        var newName = $"{baseName} {L("Preset.DuplicateSuffix")}";
        var counter = 1;
        while (Presets.Any(p => p.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
        {
            newName = $"{baseName} {LF("Preset.DuplicateSuffixWithNumber", counter)}";
            counter++;
        }

        var newPreset = SelectedPreset.Clone();
        newPreset.Name = newName;

        Presets.Add(newPreset);
        SavePresets();
        PresetsView.Refresh();
        SelectedPreset = newPreset;

        AddActivity(new ActivityItem(L("Activity.PresetDuplicatedTitle"), LF("Msg.PresetDuplicated", newName)));
        ClearError();
    }

    private void ApplyPreset()
    {
        if (SelectedAdapter == null || SelectedPreset == null)
        {
            return;
        }

        if (SelectedAdapter.Adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
        {
            var result = MessageBox.Show(
                L("Msg.ConfirmWireless"),
                L("Msg.ConfirmTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        try
        {
            NetworkManager.ApplyPreset(SelectedAdapter.Adapter, SelectedPreset);
            RefreshAdapters();

            var message = SelectedPreset.IsDhcp
                ? L("Msg.ApplyDhcpComplete")
                : L("Msg.ApplyComplete");

            MessageBox.Show(message, L("Msg.DoneTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            AddActivity(new ActivityItem(L("Msg.ApplyTitle"), LF("Msg.ApplyDetail", SelectedPreset.Name, SelectedAdapter.Name), ActivityLevel.Success));
            ClearError();
        }
        catch (Exception ex)
        {
            var details = $"{L("Label.Adapter")}: {SelectedAdapter.Name}\n" +
                          $"{L("Label.Preset")}: {SelectedPreset.Name}\n" +
                          $"{L("Main.Label.IpAddress")}: {SelectedPreset.IP}\n" +
                          $"{L("Main.Label.Subnet")}: {SelectedPreset.Subnet}\n" +
                          $"{L("Main.Label.Gateway")}: {SelectedPreset.Gateway}\n" +
                          $"{L("Main.Label.Dns1")}: {SelectedPreset.DNS1}\n" +
                          $"{L("Main.Label.Dns2")}: {SelectedPreset.DNS2}\n\n" +
                          $"{L("Msg.ErrorDetailHeader")}\n{ex.Message}\n\n" +
                          $"{L("Msg.AdapterDetailHeader")}\n{NetworkManager.GetAdapterDetailedInfo(SelectedAdapter.Adapter)}";

            MessageBox.Show(details, L("Msg.ApplyFailedTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            AddActivity(new ActivityItem(L("Msg.ApplyFailedTitle"), ex.Message, ActivityLevel.Error));
            SetErrorFromMessage(L("Msg.ApplyFailedTitle"), ex.Message);
        }
    }

    private void SyncPingTargetFromAdapter()
    {
        if (SelectedAdapter == null)
        {
            _lastAdapterId = null;
            _lastAdapterIp = string.Empty;
            if (!string.IsNullOrWhiteSpace(PingTarget))
            {
                PingTarget = string.Empty;
            }
            return;
        }

        var adapterId = SelectedAdapter.Id;
        var adapterIp = SelectedAdapter.Ipv4Address;
        var adapterChanged = !string.Equals(_lastAdapterId, adapterId, StringComparison.OrdinalIgnoreCase);
        var ipChanged = !string.Equals(_lastAdapterIp, adapterIp, StringComparison.OrdinalIgnoreCase);

        if (adapterChanged || ipChanged || string.IsNullOrWhiteSpace(PingTarget) || !_pingTargetDirty)
        {
            SetPingTarget(adapterIp, adapterChanged || ipChanged);
        }

        _lastAdapterId = adapterId;
        _lastAdapterIp = adapterIp;
    }

    private void SetPingTarget(string value, bool resetDirty)
    {
        _suppressPingTargetDirty = true;
        PingTarget = value;
        _suppressPingTargetDirty = false;
        if (resetDirty)
        {
            _pingTargetDirty = false;
        }
    }

    private async Task PingTargetAsync()
    {
        var target = PingTarget?.Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        IsPinging = true;
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(target, 3000).ConfigureAwait(true);

            if (reply.Status == IPStatus.Success)
            {
                AddActivity(new ActivityItem(L("Activity.PingTitle"),
                    LF("Msg.PingSuccess", reply.Address, reply.RoundtripTime), ActivityLevel.Success));
            }
            else
            {
                AddActivity(new ActivityItem(L("Activity.PingTitle"),
                    LF("Msg.PingFailed", reply.Status), ActivityLevel.Warning));
            }
        }
        catch (Exception ex)
        {
            AddActivity(new ActivityItem(L("Activity.PingTitle"),
                LF("Msg.PingFailed", ex.Message), ActivityLevel.Error));
        }
        finally
        {
            IsPinging = false;
        }
    }

    private void OpenPresetsFolder()
    {
        try
        {
            var folder = Path.GetDirectoryName(_activePresetsFilePath);
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                MessageBox.Show(L("Msg.OpenFolderMissing"), L("Msg.ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(LF("Msg.OpenFolderFailed", ex.Message), L("Msg.ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetErrorFromMessage(string title, string message)
    {
        ErrorTitle = title;

        var detail = message;
        var cause = string.Empty;
        var fix = string.Empty;

        var causeIndex = FindMarkerIndex(message, new[] { L("Error.Marker.Cause"), "Cause:" }, out var causeMarker);
        var fixIndex = FindMarkerIndex(message, new[] { L("Error.Marker.Fix"), "Fix:" }, out var fixMarker);

        if (causeIndex >= 0)
        {
            detail = message[..causeIndex].Trim();

            if (fixIndex > causeIndex)
            {
                var causeStart = causeIndex + causeMarker.Length;
                cause = message.Substring(causeStart, fixIndex - causeStart).Trim();
                fix = message[(fixIndex + fixMarker.Length)..].Trim();
            }
            else
            {
                cause = message[(causeIndex + causeMarker.Length)..].Trim();
            }
        }
        else if (fixIndex >= 0)
        {
            detail = message[..fixIndex].Trim();
            fix = message[(fixIndex + fixMarker.Length)..].Trim();
        }

        ErrorDetail = detail;
        ErrorCause = cause;
        ErrorFix = fix;
        IsErrorVisible = true;
    }

    private static int FindMarkerIndex(string message, string[] markers, out string marker)
    {
        foreach (var candidate in markers)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var index = message.IndexOf(candidate, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                marker = candidate;
                return index;
            }
        }

        marker = string.Empty;
        return -1;
    }

    private void ClearError()
    {
        IsErrorVisible = false;
        ErrorTitle = string.Empty;
        ErrorDetail = string.Empty;
        ErrorCause = string.Empty;
        ErrorFix = string.Empty;
    }

    private void AddActivity(ActivityItem item)
    {
        Activity.Insert(0, item);
        while (Activity.Count > ActivityMaxItems)
        {
            Activity.RemoveAt(Activity.Count - 1);
        }
        WriteActivityLog(item);
    }

    private void WriteActivityLog(ActivityItem item)
    {
        try
        {
            var line = $"{item.Timestamp:yyyy-MM-dd HH:mm:ss}\t{item.Level}\t{item.Title}\t{item.Detail}{Environment.NewLine}";
            File.AppendAllText(_activityLogPath, line, new UTF8Encoding(false));
        }
        catch
        {
            // Ignore log write failures.
        }
    }

    private bool ValidatePreset(NetworkPreset preset)
    {
        if (string.IsNullOrWhiteSpace(preset.Name))
        {
            MessageBox.Show(L("Msg.ErrorEmptyName"), L("Msg.ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        if (!preset.IsDhcp && string.IsNullOrWhiteSpace(preset.IP))
        {
            MessageBox.Show(L("Msg.ErrorEmptyIp"), L("Msg.ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        if (!preset.IsDhcp && !IsValidIpv4(preset.IP))
        {
            MessageBox.Show(L("Msg.ErrorInvalidIp"), L("Msg.ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        if (!preset.IsDhcp && !IsValidSubnetMask(preset.Subnet))
        {
            MessageBox.Show(L("Msg.ErrorInvalidSubnet"), L("Msg.ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        if (!preset.IsDhcp && !IsValidIpv4Optional(preset.Gateway))
        {
            MessageBox.Show(L("Msg.ErrorInvalidGateway"), L("Msg.ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        if (!preset.IsDhcp && !IsValidIpv4Optional(preset.DNS1))
        {
            MessageBox.Show(L("Msg.ErrorInvalidDns1"), L("Msg.ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        if (!preset.IsDhcp && !IsValidIpv4Optional(preset.DNS2))
        {
            MessageBox.Show(L("Msg.ErrorInvalidDns2"), L("Msg.ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        return true;
    }

    private static bool IsValidIpv4(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return IPAddress.TryParse(value.Trim(), out var ip) &&
               ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
    }

    private static bool IsValidIpv4Optional(string value)
    {
        return string.IsNullOrWhiteSpace(value) || IsValidIpv4(value);
    }

    private static bool IsValidSubnetMask(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!IPAddress.TryParse(value.Trim(), out var ip) ||
            ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = ip.GetAddressBytes();
        uint mask = ((uint)bytes[0] << 24) |
                    ((uint)bytes[1] << 16) |
                    ((uint)bytes[2] << 8) |
                    bytes[3];

        if (mask == 0 || mask == uint.MaxValue)
        {
            return false;
        }

        return (mask | (mask - 1)) == uint.MaxValue;
    }

    private void LoadPresets()
    {
        var presetsPath = _primaryPresetsFilePath;
        SetActivePresetsPath(_primaryPresetsFilePath);
        if (!File.Exists(presetsPath))
        {
            AppLocalization.SetLanguage("en-US");
            return;
        }

        try
        {
            var rows = ReadCsvRows(presetsPath, out var encodingMode);
            Presets.Clear();

            var language = string.Empty;
            var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var startIndex = 0;
            var foundSettings = false;
            var needsRewrite = false;

            if (encodingMode == CsvEncodingMode.Cp932Fallback)
            {
                ShowEncodingFallbackWarning();
            }

            if (rows.Count > 0 && LooksLikeHeader(rows[0]))
            {
                headerMap = BuildHeaderMap(rows[0]);
                startIndex = 1;
                if (!headerMap.ContainsKey("Language"))
                {
                    ShowLanguageColumnWarning();
                    needsRewrite = true;
                }
            }
            else
            {
                ShowCsvHeaderMissingWarning();
                needsRewrite = true;

                var firstData = rows.FirstOrDefault(r => r.Any(value => !string.IsNullOrWhiteSpace(value)));
                if (firstData != null && IsTypeRow(SafeGet(firstData, 0)))
                {
                    headerMap = DefaultCsvMap;
                    startIndex = 0;
                }
                else
                {
                    ParseLegacyRows(rows);
                    AppLocalization.SetLanguage("en-US");
                    PresetsView.Refresh();
                    SavePresets();
                    return;
                }
            }

            for (var i = startIndex; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.Length == 0 || row.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                ParseTypedRow(row, headerMap, ref language, ref foundSettings);
            }

            if (!foundSettings)
            {
                ShowSettingsRowMissingWarning();
                needsRewrite = true;
            }

            if (string.IsNullOrWhiteSpace(language))
            {
                AppLocalization.SetLanguage("en-US");
            }
            else
            {
                AppLocalization.SetLanguage(language.Trim());
            }

            PresetsView.Refresh();
            if (needsRewrite)
            {
                SavePresets();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(LF("Msg.LoadPresetsFailed", ex.Message), L("Msg.ErrorTitle"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SavePresets()
    {
        try
        {
            var builder = new StringBuilder();
            builder.AppendLine(ToCsvLine(CsvHeader));
            builder.AppendLine(ToCsvLine(new[]
            {
                PresetTypeSettings,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                AppLocalization.CurrentLanguage
            }));

            foreach (var preset in Presets)
            {
                builder.AppendLine(ToCsvLine(new[]
                {
                    PresetTypePreset,
                    preset.Name,
                    preset.Group,
                    preset.IP,
                    preset.Subnet,
                    preset.Gateway,
                    preset.DNS1,
                    preset.DNS2,
                    preset.Comment,
                    string.Empty
                }));
            }

            var contents = builder.ToString();
            if (TryWritePresets(_activePresetsFilePath, contents, out var error))
            {
                return;
            }

            var failedPath = _activePresetsFilePath;
            var failureMessage = GetSaveFailureReason(error);
            MessageBox.Show(LF("Msg.SavePresetsFailed", failedPath, failureMessage), L("Msg.ErrorTitle"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            var failureMessage = GetSaveFailureReason(ex);
            MessageBox.Show(LF("Msg.SavePresetsFailed", _activePresetsFilePath, failureMessage), L("Msg.ErrorTitle"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetActivePresetsPath(string path)
    {
        _activePresetsFilePath = path;
        PresetsStoragePath = path;
    }

    private static string GetAppDirectory()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            var dir = Path.GetDirectoryName(processPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                return dir;
            }
        }

        return AppContext.BaseDirectory;
    }

    private static bool TryWritePresets(string path, string contents, out Exception? error)
    {
        string? tempPath = null;
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            tempPath = path + ".tmp";
            File.WriteAllText(tempPath, contents, new UTF8Encoding(true));

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, null);
            }
            else
            {
                File.Move(tempPath, path);
            }

            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempPath) && File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                }
            }
        }
    }

    private static List<string[]> ReadCsvRows(string path, out CsvEncodingMode encodingMode)
    {
        var text = ReadCsvTextWithFallback(path, out encodingMode);
        text = NormalizeCsvDelimiters(text);
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
                continue;
            }

            if (c == ',')
            {
                row.Add(field.ToString());
                field.Clear();
                continue;
            }

            if (c == '\r' || c == '\n')
            {
                row.Add(field.ToString());
                field.Clear();

                if (row.Any(r => !string.IsNullOrWhiteSpace(r)))
                {
                    rows.Add(row.ToArray());
                }

                row = new List<string>();

                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                continue;
            }

            field.Append(c);
        }

        row.Add(field.ToString());
        if (row.Any(r => !string.IsNullOrWhiteSpace(r)))
        {
            rows.Add(row.ToArray());
        }

        return rows;
    }

    private static string NormalizeCsvDelimiters(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var firstLineEnd = text.IndexOfAny(new[] { '\r', '\n' });
        var firstLine = firstLineEnd >= 0 ? text[..firstLineEnd] : text;
        if (firstLine.IndexOf('\t') >= 0 && firstLine.IndexOf(',') < 0)
        {
            return ReplaceTabsOutsideQuotes(text);
        }

        return text;
    }

    private static string ReplaceTabsOutsideQuotes(string text)
    {
        var builder = new StringBuilder(text.Length);
        var inQuotes = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
                {
                    builder.Append(c);
                    builder.Append(text[i + 1]);
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                builder.Append(c);
                continue;
            }

            if (c == '\t' && !inQuotes)
            {
                builder.Append(',');
                continue;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    private static string ReadCsvTextWithFallback(string path, out CsvEncodingMode encodingMode)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            encodingMode = CsvEncodingMode.Utf8Bom;
            return Encoding.UTF8.GetString(bytes);
        }

        try
        {
            var utf8 = new UTF8Encoding(false, true);
            encodingMode = CsvEncodingMode.Utf8;
            return utf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
        }

        encodingMode = CsvEncodingMode.Cp932Fallback;
        var cp932 = Encoding.GetEncoding(932);
        return cp932.GetString(bytes);
    }

    private static bool LooksLikeHeader(string[] row)
    {
        if (row.Length == 0)
        {
            return false;
        }

        var set = row.Select(value => MapHeaderKey(TrimBom(value).Trim()))
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => value!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (set.Count == 0)
        {
            return false;
        }

        return (set.Contains("Type") || set.Contains("Name")) &&
               (set.Contains("IP") || set.Contains("Subnet") || set.Contains("DNS1") || set.Contains("DNS2"));
    }

    private static Dictionary<string, int> BuildHeaderMap(string[] row)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < row.Length; i++)
        {
            var key = MapHeaderKey(TrimBom(row[i]).Trim());
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (!map.ContainsKey(key))
            {
                map[key] = i;
            }
        }

        return map;
    }

    private static string? MapHeaderKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var normalized = NormalizeHeaderKey(key);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized switch
        {
            "type" => "Type",
            "name" => "Name",
            "presetname" => "Name",
            "profile" => "Name",
            "profilename" => "Name",
            "group" => "Group",
            "category" => "Group",
            "folder" => "Group",
            "section" => "Group",
            "ip" => "IP",
            "ipaddress" => "IP",
            "ipaddr" => "IP",
            "subnet" => "Subnet",
            "subnetmask" => "Subnet",
            "mask" => "Subnet",
            "gateway" => "Gateway",
            "defaultgateway" => "Gateway",
            "gw" => "Gateway",
            "dns1" => "DNS1",
            "dnsprimary" => "DNS1",
            "primarydns" => "DNS1",
            "dnsserver1" => "DNS1",
            "dns2" => "DNS2",
            "dnssecondary" => "DNS2",
            "secondarydns" => "DNS2",
            "dnsserver2" => "DNS2",
            "comment" => "Comment",
            "memo" => "Comment",
            "note" => "Comment",
            "remarks" => "Comment",
            "language" => "Language",
            "lang" => "Language",
            _ => null
        };
    }

    private static string NormalizeHeaderKey(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }

    private void ParseTypedRow(string[] row, Dictionary<string, int> map, ref string language, ref bool foundSettings)
    {
        var type = GetFieldTrimmed(row, map, "Type");
        if (string.Equals(type, PresetTypeSettings, StringComparison.OrdinalIgnoreCase))
        {
            foundSettings = true;
            var lang = GetFieldTrimmed(row, map, "Language");
            if (!string.IsNullOrWhiteSpace(lang))
            {
                language = lang;
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(type) &&
            !string.Equals(type, PresetTypePreset, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var preset = new NetworkPreset
        {
            Name = GetField(row, map, "Name"),
            Group = GetField(row, map, "Group"),
            IP = GetField(row, map, "IP"),
            Subnet = GetField(row, map, "Subnet"),
            Gateway = GetField(row, map, "Gateway"),
            DNS1 = GetField(row, map, "DNS1"),
            DNS2 = GetField(row, map, "DNS2"),
            Comment = GetField(row, map, "Comment")
        };

        if (!string.IsNullOrWhiteSpace(preset.Name) ||
            !string.IsNullOrWhiteSpace(preset.Group) ||
            !string.IsNullOrWhiteSpace(preset.IP) ||
            !string.IsNullOrWhiteSpace(preset.Subnet) ||
            !string.IsNullOrWhiteSpace(preset.Gateway) ||
            !string.IsNullOrWhiteSpace(preset.DNS1) ||
            !string.IsNullOrWhiteSpace(preset.DNS2) ||
            !string.IsNullOrWhiteSpace(preset.Comment))
        {
            Presets.Add(preset);
        }
    }

    private static string GetField(string[] row, Dictionary<string, int> map, string key)
    {
        if (map.TryGetValue(key, out var index) && index >= 0 && index < row.Length)
        {
            return TrimBom(row[index]);
        }

        return string.Empty;
    }

    private static string GetFieldTrimmed(string[] row, Dictionary<string, int> map, string key)
    {
        return GetField(row, map, key).Trim();
    }

    private static string ToCsvLine(IEnumerable<string> fields)
    {
        return string.Join(",", fields.Select(EscapeCsv));
    }

    private static string EscapeCsv(string value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        var needsQuote = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        var sanitized = value.Replace("\"", "\"\"");
        return needsQuote ? $"\"{sanitized}\"" : sanitized;
    }

    private static string TrimBom(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value[0] == '\uFEFF' ? value.TrimStart('\uFEFF') : value;
    }

    private string GetSaveFailureReason(Exception? error)
    {
        if (error == null)
        {
            return string.Empty;
        }

        if (IsSharingViolation(error))
        {
            return L("Msg.CsvInUseReason");
        }

        if (error is UnauthorizedAccessException)
        {
            return L("Msg.CsvAccessDeniedReason");
        }

        return error.Message;
    }

    private static bool IsSharingViolation(Exception error)
    {
        if (error is not IOException ioException)
        {
            return false;
        }

        const int sharingViolation = unchecked((int)0x80070020);
        const int lockViolation = unchecked((int)0x80070021);
        return ioException.HResult == sharingViolation || ioException.HResult == lockViolation;
    }

    private void ShowCsvHeaderMissingWarning()
    {
        if (_warnedHeaderMissing)
        {
            return;
        }

        _warnedHeaderMissing = true;
        MessageBox.Show(L("Msg.CsvHeaderMissing"), L("Msg.WarningTitle"),
            MessageBoxButton.OK, MessageBoxImage.Warning);
        AddActivity(new ActivityItem(L("Msg.WarningTitle"), L("Msg.CsvHeaderMissing"), ActivityLevel.Warning));
    }

    private void ShowSettingsRowMissingWarning()
    {
        if (_warnedSettingsRowMissing)
        {
            return;
        }

        _warnedSettingsRowMissing = true;
        AddActivity(new ActivityItem(L("Msg.WarningTitle"), L("Msg.SettingsRowMissing"), ActivityLevel.Warning));
    }

    private void ShowLanguageColumnWarning()
    {
        if (_warnedLanguageColumnMissing)
        {
            return;
        }

        _warnedLanguageColumnMissing = true;
        MessageBox.Show(L("Msg.LanguageColumnMissing"), L("Msg.WarningTitle"),
            MessageBoxButton.OK, MessageBoxImage.Warning);
        AddActivity(new ActivityItem(L("Msg.WarningTitle"), L("Msg.LanguageColumnMissing"), ActivityLevel.Warning));
    }

    private void ShowEncodingFallbackWarning()
    {
        if (_warnedEncodingFallback)
        {
            return;
        }

        _warnedEncodingFallback = true;
        MessageBox.Show(L("Msg.CsvEncodingFallback"), L("Msg.WarningTitle"),
            MessageBoxButton.OK, MessageBoxImage.Warning);
        AddActivity(new ActivityItem(L("Msg.WarningTitle"), L("Msg.CsvEncodingFallback"), ActivityLevel.Warning));
    }

    private enum CsvEncodingMode
    {
        Utf8Bom,
        Utf8,
        Cp932Fallback
    }

    private static bool IsTypeRow(string typeCandidate)
    {
        return string.Equals(typeCandidate, PresetTypePreset, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(typeCandidate, PresetTypeSettings, StringComparison.OrdinalIgnoreCase);
    }

    private void ParseLegacyRows(IEnumerable<string[]> rows)
    {
        foreach (var row in rows)
        {
            if (row.Length == 0 || row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

        var preset = new NetworkPreset
        {
            Name = SafeGet(row, 0),
            Group = string.Empty,
            IP = SafeGet(row, 1),
            Subnet = SafeGet(row, 2),
            Gateway = SafeGet(row, 3),
            DNS1 = SafeGet(row, 4),
            DNS2 = SafeGet(row, 5),
                Comment = SafeGet(row, 6)
            };

            if (string.IsNullOrWhiteSpace(preset.Name) &&
                string.IsNullOrWhiteSpace(preset.IP) &&
                string.IsNullOrWhiteSpace(preset.Subnet) &&
                string.IsNullOrWhiteSpace(preset.Gateway) &&
                string.IsNullOrWhiteSpace(preset.DNS1) &&
                string.IsNullOrWhiteSpace(preset.DNS2) &&
                string.IsNullOrWhiteSpace(preset.Comment))
            {
                continue;
            }

            Presets.Add(preset);
        }
    }

    private static string SafeGet(string[] row, int index)
    {
        if (index < 0 || index >= row.Length)
        {
            return string.Empty;
        }

        return TrimBom(row[index]);
    }

    private void OpenNetworkConnections()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "control.exe",
                Arguments = "ncpa.cpl",
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            MessageBox.Show(LF("Msg.OpenNetworkSettingsFailed", ex.Message), L("Msg.ErrorTitle"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowVersionInfo()
    {
        var window = new NetworkPresetSwitcher.VersionInfoWindow
        {
            Owner = Application.Current?.MainWindow
        };
        window.ShowDialog();
    }

    private void ToggleLanguage()
    {
        AppLocalization.ToggleLanguage();
        SavePresets();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (var adapter in Adapters)
        {
            adapter.RefreshLocalizedText();
        }

        foreach (var preset in Presets)
        {
            preset.RefreshLocalizedText();
        }

        OnPropertyChanged(nameof(CurrentStatusBadge));
        OnPropertyChanged(nameof(CurrentModeBadge));
        OnPropertyChanged(nameof(CurrentIpBadge));
        OnPropertyChanged(nameof(CurrentGatewayBadge));
        OnPropertyChanged(nameof(CurrentDnsBadge));
        PresetsView.Refresh();
    }

    private void RaiseCommandStates()
    {
        ApplyCommand.RaiseCanExecuteChanged();
        EditPresetCommand.RaiseCanExecuteChanged();
        DeletePresetCommand.RaiseCanExecuteChanged();
        DuplicatePresetCommand.RaiseCanExecuteChanged();
        SavePresetCommand.RaiseCanExecuteChanged();
        CancelEditCommand.RaiseCanExecuteChanged();
        PingCommand.RaiseCanExecuteChanged();
        OpenPresetsFolderCommand.RaiseCanExecuteChanged();
    }

    private sealed class PresetNaturalComparer : IComparer
    {
        public int Compare(object? x, object? y)
        {
            var leftPreset = x as NetworkPreset;
            var rightPreset = y as NetworkPreset;
            var leftGroup = NormalizeGroup(leftPreset?.Group);
            var rightGroup = NormalizeGroup(rightPreset?.Group);

            var groupCompare = StringComparer.CurrentCultureIgnoreCase.Compare(leftGroup, rightGroup);
            if (groupCompare != 0)
            {
                return groupCompare;
            }

            var leftName = leftPreset?.Name ?? string.Empty;
            var rightName = rightPreset?.Name ?? string.Empty;
            return CompareNatural(leftName, rightName);
        }

        private static string NormalizeGroup(string? group)
        {
            if (string.IsNullOrWhiteSpace(group))
            {
                return AppLocalization.T("Preset.Group.Ungrouped");
            }

            return group.Trim();
        }

        private static int CompareNatural(string left, string right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            var compareInfo = CultureInfo.CurrentCulture.CompareInfo;
            var ix = 0;
            var iy = 0;

            while (ix < left.Length && iy < right.Length)
            {
                var cx = left[ix];
                var cy = right[iy];

                if (char.IsDigit(cx) && char.IsDigit(cy))
                {
                    var startX = ix;
                    while (ix < left.Length && char.IsDigit(left[ix])) ix++;
                    var startY = iy;
                    while (iy < right.Length && char.IsDigit(right[iy])) iy++;

                    var numX = left.Substring(startX, ix - startX);
                    var numY = right.Substring(startY, iy - startY);

                    var numXTrim = numX.TrimStart('0');
                    var numYTrim = numY.TrimStart('0');

                    var lenX = numXTrim.Length;
                    var lenY = numYTrim.Length;
                    if (lenX != lenY)
                    {
                        return lenX.CompareTo(lenY);
                    }

                    var cmp = string.CompareOrdinal(numXTrim, numYTrim);
                    if (cmp != 0)
                    {
                        return cmp;
                    }

                    cmp = numX.Length.CompareTo(numY.Length);
                    if (cmp != 0)
                    {
                        return cmp;
                    }

                    continue;
                }

                var segStartX = ix;
                while (ix < left.Length && !char.IsDigit(left[ix])) ix++;
                var segStartY = iy;
                while (iy < right.Length && !char.IsDigit(right[iy])) iy++;

                var segX = left.Substring(segStartX, ix - segStartX);
                var segY = right.Substring(segStartY, iy - segStartY);

                var segCmp = compareInfo.Compare(segX, segY, CompareOptions.IgnoreCase);
                if (segCmp != 0)
                {
                    return segCmp;
                }
            }

            if (ix < left.Length)
            {
                return 1;
            }

            if (iy < right.Length)
            {
                return -1;
            }

            return 0;
        }
    }
}

