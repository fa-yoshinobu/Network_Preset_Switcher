using System.Windows;
using NetworkPresetSwitcher.ViewModels;

namespace NetworkPresetSwitcher;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}

