using System.IO;
using System.Windows;
using CvarcLogger.App.Services;
using CvarcLogger.App.ViewModels;
using CvarcLogger.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CvarcLogger.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Title = $"CVARC Logger v{AppVersion.Current}";

        string dbPath = SettingsService.ResolveActiveDatabasePath();
        LogNameText.Text = $"Log: {Path.GetFileName(dbPath)}";
        LogNameText.ToolTip = dbPath;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
        // TODO: Uncomment Hamlib check when SettingsService.IsHamlibAvailable() resolves compile issue
        // CheckHamlibAvailability();
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => Close();

    private void AwardsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var window = App.Services.GetRequiredService<AwardsWindow>();
        window.Owner = this;
        window.Show();
    }

    private void LookupMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var window = App.Services.GetRequiredService<LookupSettingsWindow>();
        window.Owner = this;
        window.ShowDialog();
    }

    private void CatControlMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var window = App.Services.GetRequiredService<CatControlWindow>();
        window.Owner = this;
        window.ShowDialog();
    }

    private void StationProfilesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var window = App.Services.GetRequiredService<StationProfileEditorWindow>();
        window.Owner = this;
        window.ShowDialog();
        _ = _viewModel.QsoEntry.InitializeAsync();
    }

    private void FileButton_Click(object sender, RoutedEventArgs e)
    {
        var window = App.Services.GetRequiredService<FileOperationsWindow>();
        window.Owner = this;
        window.ShowDialog();
    }

    private void StationButton_Click(object sender, RoutedEventArgs e) => StationProfilesMenuItem_Click(sender, e);

    private void CatButton_Click(object sender, RoutedEventArgs e) => CatControlMenuItem_Click(sender, e);

    private void LookupButton_Click(object sender, RoutedEventArgs e) => LookupMenuItem_Click(sender, e);

    private void ColumnsButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new ColumnPickerWindow(_viewModel.QsoLog) { Owner = this };
        window.ShowDialog();
    }

    private void NormalMode_Click(object sender, RoutedEventArgs e) => SwitchMode("Normal");

    private void ContestMode_Click(object sender, RoutedEventArgs e) => SwitchMode("Contest");

    private void SotaMode_Click(object sender, RoutedEventArgs e) => SwitchMode("SOTA");

    private void PotaMode_Click(object sender, RoutedEventArgs e) => SwitchMode("POTA");

    private void AllMode_Click(object sender, RoutedEventArgs e) => SwitchMode("All");

    private void SwitchMode(string modeName)
    {
        // Update active button styling
        NormalModeButton.Style = modeName == "Normal" ? Resources["SidebarButtonActiveStyle"] as Style : Resources["SidebarButtonStyle"] as Style;
        ContestModeButton.Style = modeName == "Contest" ? Resources["SidebarButtonActiveStyle"] as Style : Resources["SidebarButtonStyle"] as Style;
        SotaModeButton.Style = modeName == "SOTA" ? Resources["SidebarButtonActiveStyle"] as Style : Resources["SidebarButtonStyle"] as Style;
        PotaModeButton.Style = modeName == "POTA" ? Resources["SidebarButtonActiveStyle"] as Style : Resources["SidebarButtonStyle"] as Style;
        AllModeButton.Style = modeName == "All" ? Resources["SidebarButtonActiveStyle"] as Style : Resources["SidebarButtonStyle"] as Style;

        // Switch the entry form based on mode
        _viewModel.SwitchMode(modeName);
    }

    private void GridColumnsButton_Click(object sender, RoutedEventArgs e) => ColumnsButton_Click(sender, e);

    private void CatConnect_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("CAT Connection would be initiated here", "CAT Connect");
    }
}
