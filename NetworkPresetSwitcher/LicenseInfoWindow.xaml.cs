using System.Windows;
using NetworkPresetSwitcher.ViewModels;

namespace NetworkPresetSwitcher;

public partial class LicenseInfoWindow : Window
{
    public LicenseInfoWindow()
    {
        InitializeComponent();
        DataContext = new LicenseInfoViewModel();
    }
}

