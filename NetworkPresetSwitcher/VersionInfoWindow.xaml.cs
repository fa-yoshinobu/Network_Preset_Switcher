using System.Windows;
using System.Windows.Controls;
using NetworkPresetSwitcher.ViewModels;

namespace NetworkPresetSwitcher;

public partial class VersionInfoWindow : Window
{
    public VersionInfoWindow()
    {
        InitializeComponent();
        DataContext = new AboutViewModel();
    }

    private void LibrariesList_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (LibrariesList?.View is not GridView)
        {
            return;
        }

        const double padding = 40;
        var fixedWidth = LibraryColumn.Width + VersionColumn.Width + LicenseColumn.Width + padding;
        var available = LibrariesList.ActualWidth - fixedWidth;
        if (available > 120)
        {
            DescriptionColumn.Width = available;
        }
    }
}

