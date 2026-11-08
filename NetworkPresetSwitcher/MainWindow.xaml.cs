using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NetworkPresetSwitcher.ViewModels;

namespace NetworkPresetSwitcher;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    // Below this width the preset list and the details stack vertically and the page scrolls.
    private const double NarrowLayoutWidth = 1000;
    private const double MinWideContentHeight = 640;
    private const double NarrowPresetListHeight = 380;

    private bool? _isNarrow;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnPresetDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || ViewModel is not { } vm)
        {
            return;
        }

        if (vm.ApplyCommand.CanExecute(null))
        {
            vm.ApplyCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>Enter in an editor field moves to the next field; it never applies settings.</summary>
    private void OnEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        if (e.OriginalSource is TextBox { AcceptsReturn: true })
        {
            return;
        }

        if (e.OriginalSource is UIElement element)
        {
            element.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            e.Handled = true;
        }
    }

    private void OnMenuItemClick(object sender, RoutedEventArgs e)
    {
        MenuToggle.IsChecked = false;
    }

    private void OnMenuClosed(object? sender, System.EventArgs e)
    {
        MenuToggle.IsChecked = false;
    }

    private void OnRootScrollSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var narrow = RootScroll.ActualWidth < NarrowLayoutWidth;
        if (_isNarrow != narrow)
        {
            _isNarrow = narrow;
            ApplyLayout(narrow);
        }

        var margin = LayoutRoot.Margin.Top + LayoutRoot.Margin.Bottom;
        LayoutRoot.Height = narrow
            ? double.NaN
            : System.Math.Max(RootScroll.ActualHeight - margin, MinWideContentHeight);
    }

    private void ApplyLayout(bool narrow)
    {
        // Status card: adapter selection beside or above the current status.
        StatusGrid.ColumnDefinitions[0].Width = new GridLength(narrow ? 1 : 2, GridUnitType.Star);
        StatusGrid.ColumnDefinitions[1].Width = narrow ? new GridLength(0) : new GridLength(3, GridUnitType.Star);
        Grid.SetRow(StatusRight, narrow ? 1 : 0);
        Grid.SetColumn(StatusRight, narrow ? 0 : 1);
        StatusLeft.Margin = narrow ? new Thickness(0, 0, 0, 14) : new Thickness(0, 0, 24, 0);

        // Body: preset list beside or above the details/activity column.
        BodyLeftColumn.Width = narrow ? new GridLength(1, GridUnitType.Star) : new GridLength(340);
        BodyRightColumn.Width = narrow ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        BodyTopRow.Height = narrow ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        PresetsCard.Height = narrow ? NarrowPresetListHeight : double.NaN;
        PresetsCard.Margin = narrow ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 12, 0);
        Grid.SetRow(RightPanel, narrow ? 1 : 0);
        Grid.SetColumn(RightPanel, narrow ? 0 : 1);
    }
}
