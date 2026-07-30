using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvarcLogger.App.Services;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Awards;
using CvarcLogger.Core.Models;

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

    /// <summary>Qso.Id -> chronological log entry number (oldest QSO is 1, most recently logged is the
    /// highest number), independent of the grid's current sort/filter -- see GetLogNumber.</summary>
    private readonly Dictionary<int, int> _logNumbersByQsoId = new();

    /// <summary>Every QSO currently multi-selected in the log grid (Ctrl/Shift-right-click), kept in
    /// sync from the view's DataGrid.SelectedItems. Falls back to SelectedQso for actions when this is
    /// empty, so a plain single click still works for Delete.</summary>
    public ObservableCollection<Qso> SelectedQsos { get; } = new();

    /// <summary>Toggleable columns for the "Columns..." picker, alphabetically by display name.
    /// Callsign and Station Callsign are always shown and intentionally not included here.</summary>
    public ObservableCollection<ColumnOption> ColumnOptions { get; } = new();

    /// <summary>Raised whenever any column's visibility changes, so the view can update the DataGrid
    /// (DataGridColumn isn't part of the visual tree, so it can't bind to this directly).</summary>
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
        var columnDefinitions = new[]
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

        foreach (var (key, displayName, defaultVisible) in columnDefinitions.OrderBy(c => c.Item2, StringComparer.OrdinalIgnoreCase))
        {
            _settings.EnsureLogColumnDefault(key, defaultVisible);
            var option = new ColumnOption(key, displayName, !_settings.HiddenLogColumns.Contains(key));
            option.PropertyChanged += OnColumnOptionChanged;
            ColumnOptions.Add(option);
        }
    }

    public bool IsColumnVisible(string key) =>
        ColumnOptions.FirstOrDefault(c => c.Key == key)?.IsVisible ?? true;

    /// <summary>"All" button in the Columns picker: shows every toggleable column. Setting each
    /// option's IsVisible fires OnColumnOptionChanged the same as a manual checkbox click, so the grid,
    /// entry form, and saved settings all update through the existing path.</summary>
    [RelayCommand]
    private void SelectAllColumns()
    {
        foreach (var option in ColumnOptions) option.IsVisible = true;
    }

    /// <summary>"None" button in the Columns picker: hides every toggleable column. Callsign and Station
    /// Callsign are always shown and aren't in ColumnOptions, so they stay visible.</summary>
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

        if (option.IsVisible) _settings.HiddenLogColumns.Remove(option.Key);
        else _settings.HiddenLogColumns.Add(option.Key);
        _settings.SaveHiddenLogColumns();

        ColumnVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnSearchTextChanged(string? value) => QsosView.Refresh();

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
        _logNumbersByQsoId.Clear();
        int number = 1;
        foreach (var q in all.OrderBy(q => q.QsoDateTimeOnUtc).ThenBy(q => q.Id))
            _logNumbersByQsoId[q.Id] = number++;
    }

    /// <summary>Row header number shown in the log grid: 1 for the oldest QSO, counting up to the most
    /// recently logged one. Stable across sorting/filtering the grid, since it's keyed off the QSO's own
    /// timestamp rather than its current position in the view.</summary>
    public int GetLogNumber(Qso qso) => _logNumbersByQsoId.TryGetValue(qso.Id, out var n) ? n : 0;

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
