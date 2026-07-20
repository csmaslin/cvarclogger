using System.ComponentModel;
using System.Windows;
using CvarcLogger.App.Services;
using CvarcLogger.App.ViewModels;

namespace CvarcLogger.App.Views;

public partial class StationProfileEditorWindow : Window
{
    private readonly StationProfileViewModel _viewModel;
    private readonly DialogService _dialogService;

    public StationProfileEditorWindow(StationProfileViewModel viewModel, DialogService dialogService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _dialogService = dialogService;
        DataContext = _viewModel;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);

        if (_viewModel.Profiles.Count == 0)
            _viewModel.NewProfileCommand.Execute(null);
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_viewModel.Profiles.Count == 0)
        {
            e.Cancel = true;
            _dialogService.ShowError("Enter and save a station profile (callsign and UTC offset) before continuing.");
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
