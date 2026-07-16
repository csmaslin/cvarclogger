using System.Windows;
using CvarcLogger.App.ViewModels;

namespace CvarcLogger.App.Views;

public partial class StationProfileEditorWindow : Window
{
    private readonly StationProfileViewModel _viewModel;

    public StationProfileEditorWindow(StationProfileViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
