using System.ComponentModel;
using System.IO;
using System.Windows;
using CvarcLogger.App.Services;
using CvarcLogger.App.ViewModels;
using CvarcLogger.App.Views;
using CvarcLogger.Core.UiStandards;
using Microsoft.Extensions.DependencyInjection;

namespace CvarcLogger.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    // Non-modal (see ColumnsButton_Click) so the operator can leave it open, switch back to the entry
    // form to drag fields around, then return to it without closing/reopening. Tracked here so a second
    // click while it's already open activates the existing window instead of spawning a duplicate.
    private ColumnPickerWindow? _columnPickerWindow;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Title = $"CVARC Logger v{AppVersion.Current}";

        string dbPath = SettingsService.ResolveActiveDatabasePath();
        LogNameText.Text = $"Log: {Path.GetFileName(dbPath)}";
        LogNameText.ToolTip = dbPath;

        // The sidebar's "active mode" highlight has to react to SelectedEntryModeOption changing from
        // *any* source, not just a sidebar button click -- picking a different tab in the Column
        // Visibility picker now also changes the live mode (see MainViewModel's PickerModeTabChanged
        // wiring), and the highlight needs to follow that too.
        _viewModel.QsoEntry.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(QsoEntryViewModel.SelectedEntryModeOption))
                UpdateModeButtonStyles(_viewModel.QsoEntry.SelectedEntryModeOption.Value);
        };
        UpdateModeButtonStyles(_viewModel.QsoEntry.SelectedEntryModeOption.Value);
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
        // SelectedPickerModeTab is kept continuously in sync with the live mode by MainViewModel's
        // QsoEntry.PropertyChanged subscription, so no open-time sync is needed here -- the picker already
        // reflects whatever mode is live, whether it was already open or is being created just now.
        if (_columnPickerWindow is not null)
        {
            _columnPickerWindow.Activate();
            return;
        }

        _columnPickerWindow = new ColumnPickerWindow(_viewModel.QsoLog) { Owner = this };
        _columnPickerWindow.Closed += (_, _) => _columnPickerWindow = null;
        _columnPickerWindow.Show();
    }

    private void NormalMode_Click(object sender, RoutedEventArgs e) => SwitchMode("Normal");

    private void ContestMode_Click(object sender, RoutedEventArgs e) => SwitchMode("Contest");

    private void SotaMode_Click(object sender, RoutedEventArgs e) => SwitchMode("SOTA");

    private void PotaMode_Click(object sender, RoutedEventArgs e) => SwitchMode("POTA");

    private void AllMode_Click(object sender, RoutedEventArgs e) => SwitchMode("All");

    private void SwitchMode(string modeName)
    {
        // Sets QsoEntry.SelectedEntryModeOption, which the PropertyChanged subscription in the
        // constructor picks up to update the sidebar highlight (see UpdateModeButtonStyles) -- so a
        // sidebar click and a Column Visibility picker tab change both end up driving the highlight
        // through the exact same path, rather than this method needing its own separate styling logic.
        _viewModel.SwitchMode(modeName);
    }

    private void UpdateModeButtonStyles(QsoEntryMode mode)
    {
        NormalModeButton.Style = mode == QsoEntryMode.Normal ? Resources["SidebarButtonActiveStyle"] as Style : Resources["SidebarButtonStyle"] as Style;
        ContestModeButton.Style = mode == QsoEntryMode.Contest ? Resources["SidebarButtonActiveStyle"] as Style : Resources["SidebarButtonStyle"] as Style;
        SotaModeButton.Style = mode == QsoEntryMode.Sota ? Resources["SidebarButtonActiveStyle"] as Style : Resources["SidebarButtonStyle"] as Style;
        PotaModeButton.Style = mode == QsoEntryMode.Pota ? Resources["SidebarButtonActiveStyle"] as Style : Resources["SidebarButtonStyle"] as Style;
        AllModeButton.Style = mode == QsoEntryMode.All ? Resources["SidebarButtonActiveStyle"] as Style : Resources["SidebarButtonStyle"] as Style;
    }

    private void GridColumnsButton_Click(object sender, RoutedEventArgs e) => ColumnsButton_Click(sender, e);
}
