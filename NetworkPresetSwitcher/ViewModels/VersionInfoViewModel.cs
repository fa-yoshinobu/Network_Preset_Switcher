using System.Collections.ObjectModel;
using NetworkPresetSwitcher.Infrastructure;
using NetworkPresetSwitcher.Models;

namespace NetworkPresetSwitcher.ViewModels;

public sealed class VersionInfoViewModel
{
    public VersionInfoViewModel()
    {
        Libraries = new ObservableCollection<LibraryInfo>(LibraryCatalog.GetAll());
    }

    public string AppName => Localization.T("App.Title");
    public string AppVersion => LibraryCatalog.GetApplicationVersion();
    public string RuntimeVersion => LibraryCatalog.GetRuntimeVersion();

    public string AppTitle => $"{AppName} v{AppVersion}";
    public string RuntimeTitle => Localization.Format("Version.Runtime", RuntimeVersion);

    public ObservableCollection<LibraryInfo> Libraries { get; }
}

