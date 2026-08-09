using System.Windows;
using System.Windows.Input;
using CvarcLogger.App.ViewModels;

namespace CvarcLogger.App.Views;

/// <summary>Not DI-registered — it just binds to the caller's existing QsoLogViewModel, no
/// dependencies of its own.</summary>
public partial class ColumnPickerWindow : Window
{
    private readonly QsoLogViewModel _viewModel;

    public ColumnPickerWindow(QsoLogViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void RenameTab_Click(object sender, RoutedEventArgs e)
    {
        var tab = _viewModel.SelectedPickerModeTab;
        if (!tab.IsRenameable) return;

        var dialog = new RenameModeDialog(tab.Label) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Result is { } newLabel)
            _viewModel.RenameTab(tab, newLabel);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // The checkbox area scrolls horizontally (see the ScrollViewer's comment in the XAML), but a plain
    // mouse wheel only drives vertical scrolling by default -- without this, wheel input only did
    // anything while the pointer was directly over the horizontal scrollbar itself (its own native wheel
    // handling), not anywhere over the checkboxes like the operator would expect.
    private void ColumnsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ColumnsScrollViewer.ScrollToHorizontalOffset(ColumnsScrollViewer.HorizontalOffset - e.Delta);
        e.Handled = true;
    }
}
