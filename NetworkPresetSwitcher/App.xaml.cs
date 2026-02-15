using System.Text;
using System.Windows;
using AppLocalization = NetworkPresetSwitcher.Infrastructure.Localization;

namespace NetworkPresetSwitcher;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        AppLocalization.Initialize();
        base.OnStartup(e);
    }
}

