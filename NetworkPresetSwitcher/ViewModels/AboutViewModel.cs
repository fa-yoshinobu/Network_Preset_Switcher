using System.Diagnostics;
using NetworkPresetSwitcher.Infrastructure;
using NetworkPresetSwitcher.Models;

namespace NetworkPresetSwitcher.ViewModels;

public sealed class AboutViewModel : LicenseInfoViewModel
{
    public const string AuthorName = "fa-yoshinobu";
    public const string ProjectUrl = "https://github.com/fa-yoshinobu/Network_Preset_Switcher";

    public string AppName => Localization.T("App.Title");
    public string AppVersion => LibraryCatalog.GetApplicationVersion();
    public string RuntimeVersion => LibraryCatalog.GetRuntimeVersion();

    public string AppTitle => $"{AppName} v{AppVersion}";
    public string RuntimeTitle => Localization.Format("Version.Runtime", RuntimeVersion);
    public string AuthorTitle => $"{Localization.T("Version.Label.Author")} {AuthorName}";
    public string ProjectUrlLabel => Localization.T("Version.Label.GitHub");
    public string ProjectUrlDisplay => ProjectUrl;

    public RelayCommand OpenProjectUrlCommand { get; }

    public AboutViewModel()
    {
        OpenProjectUrlCommand = new RelayCommand(_ => OpenProjectUrl(), _ => true);
    }

    private static void OpenProjectUrl()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ProjectUrl,
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }
}

