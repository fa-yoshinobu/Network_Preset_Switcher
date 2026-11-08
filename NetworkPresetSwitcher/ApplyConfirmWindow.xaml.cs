using System.Windows;
using Localization = NetworkPresetSwitcher.Infrastructure.Localization;
using NetworkPresetSwitcher.ViewModels;

namespace NetworkPresetSwitcher;

public partial class ApplyConfirmWindow : Window
{
    public ApplyConfirmWindow(ApplyConfirmation confirmation)
    {
        InitializeComponent();

        TitleText.Text = Localization.Format("Apply.Confirm.Title", confirmation.PresetName);
        AdapterText.Text = Localization.Format("Apply.Confirm.Adapter", confirmation.AdapterName);
        DiffList.ItemsSource = confirmation.Items;

        if (confirmation.IsWireless)
        {
            // Wi-Fi changes can drop the connection: always ask, and default to Cancel.
            WarningText.Text = Localization.T("Msg.ConfirmWireless");
            SkipCheckBox.Visibility = Visibility.Collapsed;
            CancelButton.IsDefault = true;
            Loaded += (_, _) => CancelButton.Focus();
        }
        else
        {
            WarningText.Text = Localization.T("Apply.Confirm.Interruption");
            ApplyButton.IsDefault = true;
            Loaded += (_, _) => ApplyButton.Focus();
        }
    }

    public bool SkipFutureConfirmation => SkipCheckBox.IsChecked == true;

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
