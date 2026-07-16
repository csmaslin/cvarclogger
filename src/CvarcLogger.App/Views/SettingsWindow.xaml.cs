using System.Windows;
using CvarcLogger.App.ViewModels;

namespace CvarcLogger.App.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private async void SaveCredentials_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.QrzPassword = PasswordBox.Password;
        await _viewModel.SaveQrzCredentialsCommand.ExecuteAsync(null);
        PasswordBox.Clear();
    }

    private async void SaveQrzCqCredentials_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.QrzCqPassword = QrzCqPasswordBox.Password;
        await _viewModel.SaveQrzCqCredentialsCommand.ExecuteAsync(null);
        QrzCqPasswordBox.Clear();
    }
}
