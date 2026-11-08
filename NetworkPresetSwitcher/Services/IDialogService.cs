using System.Windows;
using NetworkPresetSwitcher.ViewModels;

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

    /// <summary>
    /// Shows the apply confirmation with the current/after comparison.
    /// Returns true to apply. <paramref name="skipFutureConfirmation"/> is true when the user
    /// chose not to be asked again (offered for wired adapters only).
    /// </summary>
    bool ConfirmApply(ApplyConfirmation confirmation, out bool skipFutureConfirmation);
}
