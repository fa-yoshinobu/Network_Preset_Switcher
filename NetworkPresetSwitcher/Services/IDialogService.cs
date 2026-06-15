using System.Windows;

namespace NetworkPresetSwitcher.Services;

public interface IDialogService
{
    MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon);

    MessageBoxResult Show(
        string messageBoxText,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon,
        MessageBoxResult defaultResult);
}

