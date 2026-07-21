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

        // PasswordBox.Password can't be data-bound, so the loaded credential (see
        // SettingsViewModel.InitializeAsync) has to be pushed in here explicitly. Stays masked -- this
        // only makes "Show password" able to reveal the saved value, it doesn't display it directly.
        QrzPasswordBox.Password = _viewModel.QrzPassword;
        QrzCqPasswordBox.Password = _viewModel.QrzCqPassword;
    }

    private async void SaveCredentials_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.QrzPassword = QrzShowPasswordCheckBox.IsChecked == true ? QrzPasswordTextBox.Text : QrzPasswordBox.Password;
        await _viewModel.SaveQrzCredentialsCommand.ExecuteAsync(null);
        QrzPasswordBox.Clear();
        QrzPasswordTextBox.Clear();
    }

    private async void SaveQrzCqCredentials_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.QrzCqPassword = QrzCqShowPasswordCheckBox.IsChecked == true ? QrzCqPasswordTextBox.Text : QrzCqPasswordBox.Password;
        await _viewModel.SaveQrzCqCredentialsCommand.ExecuteAsync(null);
        QrzCqPasswordBox.Clear();
        QrzCqPasswordTextBox.Clear();
    }

    // PasswordBox.Password can't be data-bound (by design, so a plaintext password doesn't linger
    // in a bindable property longer than necessary), so revealing it means swapping in a plain
    // TextBox showing the same text instead -- these handlers keep the two controls in sync across
    // the swap in both directions.
    private void QrzShowPassword_Checked(object sender, RoutedEventArgs e)
    {
        QrzPasswordTextBox.Text = QrzPasswordBox.Password;
        QrzPasswordBox.Visibility = Visibility.Collapsed;
        QrzPasswordTextBox.Visibility = Visibility.Visible;
    }

    private void QrzShowPassword_Unchecked(object sender, RoutedEventArgs e)
    {
        QrzPasswordBox.Password = QrzPasswordTextBox.Text;
        QrzPasswordTextBox.Visibility = Visibility.Collapsed;
        QrzPasswordBox.Visibility = Visibility.Visible;
    }

    private void QrzCqShowPassword_Checked(object sender, RoutedEventArgs e)
    {
        QrzCqPasswordTextBox.Text = QrzCqPasswordBox.Password;
        QrzCqPasswordBox.Visibility = Visibility.Collapsed;
        QrzCqPasswordTextBox.Visibility = Visibility.Visible;
    }

    private void QrzCqShowPassword_Unchecked(object sender, RoutedEventArgs e)
    {
        QrzCqPasswordBox.Password = QrzCqPasswordTextBox.Text;
        QrzCqPasswordTextBox.Visibility = Visibility.Collapsed;
        QrzCqPasswordBox.Visibility = Visibility.Visible;
    }
}
