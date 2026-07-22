using System.Windows;
using CvarcLogger.App.ViewModels;

namespace CvarcLogger.App.Views;

public partial class CatControlWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public CatControlWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();

        // PasswordBox.Password can't be data-bound, so the loaded Internet Control password (see
        // SettingsViewModel.InitializeAsync) is pushed in here explicitly. Stays masked -- this only lets
        // "Show password" reveal the saved value; it isn't displayed directly.
        InternetPasswordBox.Password = _viewModel.InternetRadioPassword;
    }

    private async void SaveCatSettings_Click(object sender, RoutedEventArgs e)
    {
        // Grab the Internet password from the PasswordBox (or its shown-text twin) before the combined
        // save, since PasswordBox.Password can't be data-bound. Harmless when the Internet source isn't
        // in use -- an empty password just isn't written to the credential store.
        _viewModel.InternetRadioPassword = InternetShowPasswordCheckBox.IsChecked == true
            ? InternetPasswordTextBox.Text
            : InternetPasswordBox.Password;
        await _viewModel.SaveCatSettingsCommand.ExecuteAsync(null);
        InternetPasswordBox.Clear();
        InternetPasswordTextBox.Clear();
    }

    // PasswordBox.Password can't be data-bound (by design), so revealing it means swapping in a plain
    // TextBox showing the same text -- these handlers keep the two controls in sync across the swap.
    private void InternetShowPassword_Checked(object sender, RoutedEventArgs e)
    {
        InternetPasswordTextBox.Text = InternetPasswordBox.Password;
        InternetPasswordBox.Visibility = Visibility.Collapsed;
        InternetPasswordTextBox.Visibility = Visibility.Visible;
    }

    private void InternetShowPassword_Unchecked(object sender, RoutedEventArgs e)
    {
        InternetPasswordBox.Password = InternetPasswordTextBox.Text;
        InternetPasswordTextBox.Visibility = Visibility.Collapsed;
        InternetPasswordBox.Visibility = Visibility.Visible;
    }
}
