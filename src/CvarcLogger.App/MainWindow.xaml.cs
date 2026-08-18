using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
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
        // Before InitializeAsync rather than after: restoring the split is pure layout and shouldn't
        // wait behind the database load, otherwise the panes visibly jump from the 50/50 default to the
        // saved position once the log finishes loading.
        RestoreEntryLogSplit();
        await _viewModel.InitializeAsync();
        // TODO: Uncomment Hamlib check when SettingsService.IsHamlibAvailable() resolves compile issue
        // CheckHamlibAvailability();
    }

    private void Window_Closing(object sender, CancelEventArgs e) => SaveEntryLogSplit();

    /// <summary>Also saves on drag release, not just on exit, so a crash or a forced kill doesn't cost
    /// the operator the layout they just set up.</summary>
    private void EntryLogSplitter_DragCompleted(object sender, DragCompletedEventArgs e) => SaveEntryLogSplit();

    /// <summary>Applies the saved entry form / log grid split. A fresh install has nothing saved, which
    /// leaves the even 50/50 split declared in XAML.</summary>
    private void RestoreEntryLogSplit()
    {
        if (App.Services.GetRequiredService<SettingsService>().EntryFormSplitRatio is not double ratio) return;

        EntryFormRow.Height = new GridLength(ratio, GridUnitType.Star);
        LogGridRow.Height = new GridLength(1 - ratio, GridUnitType.Star);
    }

    /// <summary>Records the split as the entry form's share of the two panes' combined height, measured
    /// from what's actually on screen rather than from the rows' star weights, so it stays correct even
    /// when MinHeight has clamped a pane to something other than the weight asked for.</summary>
    private void SaveEntryLogSplit()
    {
        double entryHeight = EntryFormRow.ActualHeight;
        double gridHeight = LogGridRow.ActualHeight;
        double total = entryHeight + gridHeight;

        // Zero total means the rows were never laid out (closed while minimized, or shut down before the
        // first render). Saving that would persist a meaningless ratio over a good one.
        if (total <= 0) return;

        App.Services.GetRequiredService<SettingsService>().SaveEntryFormSplitRatio(entryHeight / total);
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

    // Remembers which half of each toggle tab was last shown, so switching to a toggle tab from
    // somewhere else (the other toggle tab, All, or the Column Visibility picker) restores whichever
    // mode it was last left on instead of always resetting to the first one. Kept in sync with the live
    // mode from *any* source (not just these two click handlers) inside UpdateModeButtonStyles.
    private QsoEntryMode _normalContestLastMode = QsoEntryMode.Normal;
    private QsoEntryMode _sotaPotaLastMode = QsoEntryMode.Sota;
    private QsoEntryMode _undef1Undef2LastMode = QsoEntryMode.Net;
    private QsoEntryMode _undef3Undef4LastMode = QsoEntryMode.Custom2;

    // Toggle tabs: clicking the tab that's already active flips between its two modes; clicking it while
    // a *different* tab is active just switches to it, showing whichever of its two modes was last shown.
    private void NormalContestToggle_Click(object sender, RoutedEventArgs e)
    {
        var current = _viewModel.QsoEntry.SelectedEntryModeOption.Value;
        var isActive = current is QsoEntryMode.Normal or QsoEntryMode.Contest;
        var target = isActive
            ? (current == QsoEntryMode.Normal ? QsoEntryMode.Contest : QsoEntryMode.Normal)
            : _normalContestLastMode;
        SwitchMode(target == QsoEntryMode.Normal ? "Normal" : "Contest");
    }

    private void SotaPotaToggle_Click(object sender, RoutedEventArgs e)
    {
        var current = _viewModel.QsoEntry.SelectedEntryModeOption.Value;
        var isActive = current is QsoEntryMode.Sota or QsoEntryMode.Pota;
        var target = isActive
            ? (current == QsoEntryMode.Sota ? QsoEntryMode.Pota : QsoEntryMode.Sota)
            : _sotaPotaLastMode;
        SwitchMode(target == QsoEntryMode.Sota ? "SOTA" : "POTA");
    }

    private void Undef1Undef2Toggle_Click(object sender, RoutedEventArgs e)
    {
        var current = _viewModel.QsoEntry.SelectedEntryModeOption.Value;
        var isActive = current is QsoEntryMode.Net or QsoEntryMode.Custom1;
        var target = isActive
            ? (current == QsoEntryMode.Net ? QsoEntryMode.Custom1 : QsoEntryMode.Net)
            : _undef1Undef2LastMode;
        SwitchMode(target == QsoEntryMode.Net ? "Net" : "Custom1");
    }

    private void Undef3Undef4Toggle_Click(object sender, RoutedEventArgs e)
    {
        var current = _viewModel.QsoEntry.SelectedEntryModeOption.Value;
        var isActive = current is QsoEntryMode.Custom2 or QsoEntryMode.Custom3;
        var target = isActive
            ? (current == QsoEntryMode.Custom2 ? QsoEntryMode.Custom3 : QsoEntryMode.Custom2)
            : _undef3Undef4LastMode;
        SwitchMode(target == QsoEntryMode.Custom2 ? "Custom2" : "Custom3");
    }

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
        if (mode is QsoEntryMode.Normal or QsoEntryMode.Contest) _normalContestLastMode = mode;
        if (mode is QsoEntryMode.Sota or QsoEntryMode.Pota) _sotaPotaLastMode = mode;
        if (mode is QsoEntryMode.Net or QsoEntryMode.Custom1) _undef1Undef2LastMode = mode;
        if (mode is QsoEntryMode.Custom2 or QsoEntryMode.Custom3) _undef3Undef4LastMode = mode;

        NormalContestToggleButton.Style = mode is QsoEntryMode.Normal or QsoEntryMode.Contest
            ? Resources["SidebarButtonActiveStyle"] as Style : Resources["SidebarButtonStyle"] as Style;
        SotaPotaToggleButton.Style = mode is QsoEntryMode.Sota or QsoEntryMode.Pota
            ? Resources["SidebarButtonActiveStyle"] as Style : Resources["SidebarButtonStyle"] as Style;
        Undef1Undef2ToggleButton.Style = mode is QsoEntryMode.Net or QsoEntryMode.Custom1
            ? Resources["SidebarButtonActiveStyle"] as Style : Resources["SidebarButtonStyle"] as Style;
        Undef3Undef4ToggleButton.Style = mode is QsoEntryMode.Custom2 or QsoEntryMode.Custom3
            ? Resources["SidebarButtonActiveStyle"] as Style : Resources["SidebarButtonStyle"] as Style;
        AllModeButton.Style = mode == QsoEntryMode.All ? Resources["SidebarButtonActiveStyle"] as Style : Resources["SidebarButtonStyle"] as Style;
    }

    private void GridColumnsButton_Click(object sender, RoutedEventArgs e) => ColumnsButton_Click(sender, e);
}
