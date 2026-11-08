using System.Windows;
using NetworkPresetSwitcher.ViewModels;

namespace NetworkPresetSwitcher.Services;

public sealed class MessageBoxDialogService : IDialogService
{
    public MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
    {
        return MessageBox.Show(messageBoxText, caption, button, icon);
    }

    public MessageBoxResult Show(
        string messageBoxText,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon,
        MessageBoxResult defaultResult)
    {
        return MessageBox.Show(messageBoxText, caption, button, icon, defaultResult);
    }

    public bool ConfirmApply(ApplyConfirmation confirmation, out bool skipFutureConfirmation)
    {
        var window = new ApplyConfirmWindow(confirmation)
        {
            Owner = Application.Current?.MainWindow
        };

        var result = window.ShowDialog() == true;
        skipFutureConfirmation = result && window.SkipFutureConfirmation;
        return result;
    }
}
