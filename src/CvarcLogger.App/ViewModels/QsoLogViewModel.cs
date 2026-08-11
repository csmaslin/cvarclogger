using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvarcLogger.App.Services;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Awards;
using CvarcLogger.Core.Models;
using CvarcLogger.Core.UiStandards;

namespace CvarcLogger.App.ViewModels;

public partial class QsoLogViewModel : ObservableObject
{
    private readonly IQsoRepository _qsoRepository;
    private readonly ICallsignEntityResolver _entityResolver;
    private readonly DialogService _dialogService;
    private readonly SettingsService _settings;

    [ObservableProperty] private string? searchText;
    [ObservableProperty] private Qso? selectedQso;

    public ObservableCollection<Qso> Qsos { get; } = new();
    public ICollectionView QsosView { get; }

    /// <summary>Every QSO currently multi-selected in the log grid (Ctrl/Shift-right-click), kept in
    /// sync from the view's DataGrid.SelectedItems. Falls back to SelectedQso for actions when this is
    /// empty, so a plain single click still works for Delete.</summary>
    public ObservableCollection<Qso> SelectedQsos { get; } = new();

    /// <summary>Toggleable columns for the currently selected picker tab (SelectedPickerModeTab),
    /// alphabetically by display name. Callsign and Station Callsign are always shown and intentionally
    /// not included here. Rebuilt by RebuildColumnOptions whenever the tab selection changes -- each tab
    /// edits its own mode's independent hidden-columns set (SettingsService.GetHiddenColumns).</summary>
    public ObservableCollection<ColumnOption> ColumnOptions { get; } = new();

    /// <summary>Tabs for the Column Visibility picker: Normal/Contest/SOTA/POTA (each independently
    /// renameable, see ColumnPickerModeTab) plus a static "All" catch-all. Net is excluded -- it isn't
    /// wired into any UI yet (see QsoEntryModeOptions), so there's nothing to give it a tab for.</summary>
    public ObservableCollection<ColumnPickerModeTab> PickerModeTabs { get; } = new();

    [ObservableProperty] private ColumnPickerModeTab selectedPickerModeTab = null!;

    private readonly (string Key, string DisplayName, bool DefaultVisible)[] _columnDefinitions;

    /// <summary>The app's actual active Log Entry Mode, kept in sync by MainViewModel via SetLiveMode
    /// whenever QsoEntryViewModel.SelectedEntryModeOption changes. Drives IsColumnVisible (and so the
    /// grid's real rendering) independently of SelectedPickerModeTab, which only controls what the picker
    /// UI is currently showing/editing -- they're the same mode most of the time but don't have to be
    /// (e.g. editing SOTA's field list while still in Normal mode).</summary>
    private string _liveMode = QsoEntryMode.Normal.ToString();

    /// <summary>Raised whenever any column's visibility changes (a checkbox toggle, "All"/"None", or a
    /// live mode switch), so the view can update the DataGrid (DataGridColumn isn't part of the visual
    /// tree, so it can't bind to this directly).</summary>
    public event EventHandler? ColumnVisibilityChanged;

    public QsoLogViewModel(
        IQsoRepository qsoRepository,
        ICallsignEntityResolver entityResolver,
        DialogService dialogService,
        SettingsService settings)
    {
        _qsoRepository = qsoRepository;
        _entityResolver = entityResolver;
        _dialogService = dialogService;
        _settings = settings;
        QsosView = CollectionViewSource.GetDefaultView(Qsos);
        QsosView.Filter = FilterQso;

        // defaultVisible=false for every column added after the original 12 keeps existing users'
        // grids exactly as they were — SettingsService.EnsureLogColumnDefault only applies a column's
        // default the first time that key is ever seen for a given settings file.
        _columnDefinitions = new[]
        {
            ("UtcTime", "Date/Time (UTC)", true),
            ("LocalTime", "Local Time", true), ("Band", "Band", true), ("Mode", "Mode", true),
            ("Freq", "Freq", true), ("Rst", "RST S/R", true), ("Name", "Name", true),
            ("Grid", "Grid", true), ("City", "City", true), ("State", "State", true),
            ("Country", "Country", true), ("Qsl", "QSL S/R", true),
            ("County", "County", false), ("ArrlSection", "ARRL Section", false),
            ("CqZone", "CQ Zone", false), ("ItuZone", "ITU Zone", false),
            ("Continent", "Continent", false), ("SubMode", "Sub Mode", false),
            ("FreqRx", "Freq Rx", false), ("TxPower", "TX Power", false),
            ("QslVia", "QSL Via", false), ("Lotw", "LoTW S/R", false), ("Comment", "Comment", false),
            ("TimeOff", "Time Off (UTC)", false),
            ("Operator", "Operator", false), ("MyGrid", "My Grid", false),
            ("MyState", "My State", false), ("MyCounty", "My County", false),
            ("Qth", "QTH", false), ("Op", "OP", false),
            ("MySota", "My SOTA", false), ("Sota", "SOTA", false),
            ("MyPota", "My POTA", false), ("Pota", "POTA", false),
            ("Precedence", "Precedence", false), ("Check", "Check", false), ("Class", "Class", false),
            ("Skcc", "SKCC #", false), ("MySkcc", "My SKCC #", false),
            ("Sequence", "Seq #", false),
        };

        foreach (var (key, _, defaultVisible) in _columnDefinitions)
            _settings.EnsureLogColumnDefault(key, defaultVisible);

        foreach (var mode in new[]
                 {
                     QsoEntryMode.Normal, QsoEntryMode.Contest, QsoEntryMode.Sota, QsoEntryMode.Pota,
                     QsoEntryMode.Net, QsoEntryMode.Custom1, QsoEntryMode.Custom2, QsoEntryMode.Custom3,
                 })
        {
            var modeKey = mode.ToString();
            PickerModeTabs.Add(new ColumnPickerModeTab(mode, _settings.GetModeTabLabel(modeKey, DefaultTabLabel(mode)), isRenameable: true));
        }
        PickerModeTabs.Add(new ColumnPickerModeTab(QsoEntryMode.All, "All", isRenameable: false));

        SelectedPickerModeTab = PickerModeTabs[0]; // Normal -- setter fires RebuildColumnOptions via the partial method below.
    }

    private static string DefaultTabLabel(QsoEntryMode mode) => mode switch
    {
        QsoEntryMode.Sota => "SOTA",
        QsoEntryMode.Pota => "POTA",
        QsoEntryMode.Net => "Undef-1",
        QsoEntryMode.Custom1 => "Undef-2",
        QsoEntryMode.Custom2 => "Undef-3",
        QsoEntryMode.Custom3 => "Undef-4",
        _ => mode.ToString(),
    };

    /// <summary>Raised when the operator picks a different tab in the Column Visibility picker, so
    /// MainViewModel can switch the app's actual live mode to match (sidebar highlight included) -- full
    /// two-way sync with the sidebar's mode buttons, per the operator's explicit request: picking a tab in
    /// the picker should feel the same as clicking that mode in the sidebar.</summary>
    public event EventHandler<QsoEntryMode>? PickerModeTabChanged;

    partial void OnSelectedPickerModeTabChanged(ColumnPickerModeTab value)
    {
        RebuildColumnOptions();
        PickerModeTabChanged?.Invoke(this, value.Value);
    }

    private void RebuildColumnOptions()
    {
        foreach (var option in ColumnOptions) option.PropertyChanged -= OnColumnOptionChanged;
        ColumnOptions.Clear();

        var hidden = _settings.GetHiddenColumns(SelectedPickerModeTab.Value.ToString());
        foreach (var (key, displayName, _) in _columnDefinitions.OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            var option = new ColumnOption(key, displayName, !hidden.Contains(key));
            option.PropertyChanged += OnColumnOptionChanged;
            ColumnOptions.Add(option);
        }
    }

    /// <summary>Persists a new display label for a picker tab (see RenameModeDialog). "All" isn't
    /// renameable (ColumnPickerModeTab.IsRenameable) -- enforced by the view disabling the Rename button
    /// for it, not re-checked here. Renaming propagates everywhere that mode's name appears (sidebar mode
    /// button, entry form title) via ModeLabelsChanged -- see MainViewModel's wiring to QsoEntry.</summary>
    public void RenameTab(ColumnPickerModeTab tab, string newLabel)
    {
        tab.Label = newLabel;
        _settings.SetModeTabLabel(tab.Value.ToString(), newLabel);
        ModeLabelsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised whenever a mode's display label changes (see RenameTab), so MainViewModel can tell
    /// QsoEntryViewModel to refresh its own label-derived properties (EntryFormTitle) -- same bridging
    /// pattern as ColumnVisibilityChanged above, since QsoEntryViewModel deliberately doesn't reference
    /// its sibling QsoLogViewModel directly.</summary>
    public event EventHandler? ModeLabelsChanged;

    public bool IsColumnVisible(string key) => !_settings.GetHiddenColumns(_liveMode).Contains(key);

    /// <summary>Called by MainViewModel whenever QsoEntryViewModel.SelectedEntryModeOption changes, so
    /// the log grid's rendered columns follow the app's actual active mode regardless of which tab
    /// happens to be open in the picker (SelectedPickerModeTab).</summary>
    public void SetLiveMode(string mode)
    {
        _liveMode = mode;
        ColumnVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>"All" button in the Columns picker: shows every column in the currently selected tab.
    /// Setting each option's IsVisible fires OnColumnOptionChanged the same as a manual checkbox click,
    /// so the grid, entry form, and saved settings all update through the existing path.</summary>
    [RelayCommand]
    private void SelectAllColumns()
    {
        foreach (var option in ColumnOptions) option.IsVisible = true;
    }

    /// <summary>"None" button in the Columns picker: hides every column in the currently selected tab.
    /// Callsign and Station Callsign are always shown and aren't in ColumnOptions, so they stay
    /// visible.</summary>
    [RelayCommand]
    private void SelectNoColumns()
    {
        foreach (var option in ColumnOptions) option.IsVisible = false;
    }

    /// <summary>Saved column display order, keyed by column key (see SettingsService.LogColumnOrder).</summary>
    public IReadOnlyDictionary<string, int> ColumnOrder => _settings.LogColumnOrder;

    /// <summary>Persists the DataGrid's current left-to-right column order so it survives an app
    /// restart.</summary>
    public void SaveColumnOrder(IReadOnlyList<string> keysInDisplayOrder)
    {
        var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < keysInDisplayOrder.Count; i++)
            order[keysInDisplayOrder[i]] = i;
        _settings.SaveLogColumnOrder(order);
    }

    /// <summary>Saved column widths, keyed by column key (see SettingsService.LogColumnWidths).</summary>
    public IReadOnlyDictionary<string, double> ColumnWidths => _settings.LogColumnWidths;

    /// <summary>Persists the DataGrid's current column widths so they survive an app restart.</summary>
    public void SaveColumnWidths(IReadOnlyDictionary<string, double> widths) => _settings.SaveLogColumnWidths(widths);

    private void OnColumnOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ColumnOption.IsVisible) || sender is not ColumnOption option) return;

        var hidden = _settings.GetHiddenColumns(SelectedPickerModeTab.Value.ToString());
        if (option.IsVisible) hidden.Remove(option.Key);
        else hidden.Add(option.Key);
        _settings.SaveHiddenColumns();

        ColumnVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnSearchTextChanged(string? value) => QsosView.Refresh();

    [RelayCommand]
    private void ClearSearch() => SearchText = null;

    private bool FilterQso(object obj)
    {
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        if (obj is not Qso qso) return false;
        return qso.Callsign.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
               || (qso.Name?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
               || (qso.GridSquare?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
               || (qso.City?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var all = await _qsoRepository.GetAllAsync();
        Qsos.Clear();
        foreach (var q in all) Qsos.Add(q);

        // Recompute chronological log numbers from this same set -- GetAllAsync returns newest-first,
        // but the log number the grid shows should count up from the oldest QSO, not the display order.
        int number = 1;
        foreach (var q in all.OrderBy(q => q.QsoDateTimeOnUtc).ThenBy(q => q.Id))
            q.LogNumber = number++;
    }

    /// <summary>Net roll-call marker: which QSOs the net controller has already called on for their
    /// statement, so a pause/interruption doesn't lose their place going down the list. Deliberately
    /// in-memory only, not persisted to the database or settings -- this is transient net-session state,
    /// not real QSO data, and Clear Net Markers (below) is meant to reset it for the next net without
    /// leaving stale flags sitting in the log forever.</summary>
    private readonly HashSet<int> _netCalledQsoIds = new();

    public bool IsNetCalled(Qso qso) => _netCalledQsoIds.Contains(qso.Id);
    public void MarkNetCalled(Qso qso) => _netCalledQsoIds.Add(qso.Id);
    public void UnmarkNetCalled(Qso qso) => _netCalledQsoIds.Remove(qso.Id);

    /// <summary>Raised after Clear Net Markers so the grid can re-sync every visible row's checkbox --
    /// the markers live outside the bound Qso collection, so nothing else would tell the grid to redraw
    /// them.</summary>
    public event EventHandler? NetCalledCleared;

    [RelayCommand]
    private void ClearNetCalled()
    {
        _netCalledQsoIds.Clear();
        NetCalledCleared?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var toDelete = SelectedQsos.Count > 0
            ? SelectedQsos.ToList()
            : SelectedQso is not null ? new List<Qso> { SelectedQso } : new List<Qso>();
        if (toDelete.Count == 0) return;

        string message = toDelete.Count == 1
            ? $"Delete the QSO with {toDelete[0].Callsign}?"
            : $"Delete {toDelete.Count} QSOs?";
        if (!_dialogService.Confirm(message)) return;

        foreach (var qso in toDelete)
        {
            await _qsoRepository.DeleteAsync(qso.Id);
            Qsos.Remove(qso);
        }
    }

    /// <summary>Logs QSOs pasted in from the clipboard (Ctrl+V on the log grid) as new records. Each
    /// one's DXCC entity is (re-)resolved from its callsign rather than trusting anything already set on
    /// the pasted-in instance -- same rationale as ADIF import (see ImportExportViewModel): an externally
    /// -sourced DXCC code isn't guaranteed to exist in our own DxccEntities table.</summary>
    public async Task AddPastedQsosAsync(IReadOnlyList<Qso> qsos)
    {
        foreach (var qso in qsos)
        {
            var resolvedEntity = await _entityResolver.ResolveAsync(qso.Callsign);
            qso.DxccEntityCode = resolvedEntity?.EntityCode;
            await _qsoRepository.AddAsync(qso);
        }
        await RefreshAsync();
    }
}
