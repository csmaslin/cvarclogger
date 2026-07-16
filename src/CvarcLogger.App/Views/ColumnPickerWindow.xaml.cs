using System.Windows;
using CvarcLogger.App.ViewModels;

namespace CvarcLogger.App.Views;

/// <summary>Not DI-registered — it just binds to the caller's existing QsoLogViewModel, no
/// dependencies of its own.</summary>
public partial class ColumnPickerWindow : Window
{
    public ColumnPickerWindow(QsoLogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
