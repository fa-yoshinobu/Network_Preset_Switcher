using System.Windows;

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
}
