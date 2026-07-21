namespace CvarcCellLog.Pages;

public partial class HomePage : ContentPage
{
    public HomePage()
    {
        InitializeComponent();
        VersionLabel.Text = $"v{AppInfo.Current.VersionString}";
    }

    private async void OnFileClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(FileMenuPage));

    private async void OnStationClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(StationProfilesPage));

    private async void OnViewClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(QsoLogPage));

    private async void OnAwardsClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(AwardsPage));

    private async void OnToolsClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(SettingsPage));
}
