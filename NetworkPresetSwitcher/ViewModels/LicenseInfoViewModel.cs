using System.Collections.ObjectModel;
using System.Linq;
using System.Diagnostics;
using NetworkPresetSwitcher.Infrastructure;
using NetworkPresetSwitcher.Models;

namespace NetworkPresetSwitcher.ViewModels;

public class LicenseInfoViewModel : ObservableObject
{
    private LibraryInfo? _selectedLibrary;
    private string _licenseText = string.Empty;

    public LicenseInfoViewModel()
    {
        Libraries = new ObservableCollection<LibraryInfo>(LibraryCatalog.GetAll());
        OpenUrlCommand = new RelayCommand(_ => OpenUrl(), _ => CanOpenUrl);

        if (Libraries.Count > 0)
        {
            var appName = LibraryCatalog.GetApplicationName();
            SelectedLibrary = Libraries.FirstOrDefault(l =>
                string.Equals(l.Name, appName, StringComparison.OrdinalIgnoreCase)) ?? Libraries[0];
        }
    }

    public ObservableCollection<LibraryInfo> Libraries { get; }

    public LibraryInfo? SelectedLibrary
    {
        get => _selectedLibrary;
        set
        {
            if (SetProperty(ref _selectedLibrary, value))
            {
                if (_selectedLibrary == null)
                {
                    LicenseText = string.Empty;
                }
                else
                {
                    var appName = LibraryCatalog.GetApplicationName();
                    LicenseText = string.Equals(_selectedLibrary.Name, appName, StringComparison.OrdinalIgnoreCase)
                        ? LibraryCatalog.GetLicenseText(_selectedLibrary)
                        : string.Empty;
                }
                OpenUrlCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(SelectedLibraryName));
                OnPropertyChanged(nameof(SelectedLibraryVersion));
                OnPropertyChanged(nameof(SelectedLibraryLicense));
            }
        }
    }

    public string LicenseText
    {
        get => _licenseText;
        private set => SetProperty(ref _licenseText, value);
    }

    public string SelectedLibraryName => SelectedLibrary?.Name ?? string.Empty;
    public string SelectedLibraryVersion => SelectedLibrary?.Version ?? string.Empty;
    public string SelectedLibraryLicense => SelectedLibrary?.License ?? string.Empty;

    public bool CanOpenUrl => !string.IsNullOrWhiteSpace(SelectedLibrary?.Url);

    public RelayCommand OpenUrlCommand { get; }

    private void OpenUrl()
    {
        if (!CanOpenUrl || SelectedLibrary == null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SelectedLibrary.Url,
                UseShellExecute = true
            });
        }
        catch
        {
            // UI can surface a message if needed.
        }
    }
}

