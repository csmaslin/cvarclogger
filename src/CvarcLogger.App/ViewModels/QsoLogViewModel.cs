using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvarcLogger.App.Services;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Models;

namespace CvarcLogger.App.ViewModels;

public partial class QsoLogViewModel : ObservableObject
{
    private readonly IQsoRepository _qsoRepository;
    private readonly DialogService _dialogService;
    private readonly SettingsService _settings;

    [ObservableProperty] private string? searchText;
    [ObservableProperty] private Qso? selectedQso;

    public ObservableCollection<Qso> Qsos { get; } = new();
    public ICollectionView QsosView { get; }

    /// <summary>Toggleable columns for the "Columns..." picker. Callsign and Date/Time (UTC) are
    /// always shown and intentionally not included here.</summary>
    public ObservableCollection<ColumnOption> ColumnOptions { get; } = new();

    /// <summary>Raised whenever any column's visibility changes, so the view can update the DataGrid
    /// (DataGridColumn isn't part of the visual tree, so it can't bind to this directly).</summary>
    public event EventHandler? ColumnVisibilityChanged;

    public QsoLogViewModel(IQsoRepository qsoRepository, DialogService dialogService, SettingsService settings)
    {
        _qsoRepository = qsoRepository;
        _dialogService = dialogService;
        _settings = settings;
        QsosView = CollectionViewSource.GetDefaultView(Qsos);
        QsosView.Filter = FilterQso;

        // defaultVisible=false for every column added after the original 12 keeps existing users'
        // grids exactly as they were — SettingsService.EnsureLogColumnDefault only applies a column's
        // default the first time that key is ever seen for a given settings file.
        foreach (var (key, displayName, defaultVisible) in new[]
        {
            ("LocalTime", "Local Time", true), ("Band", "Band", true), ("Mode", "Mode", true),
            ("Freq", "Freq", true), ("Rst", "RST S/R", true), ("Name", "Name", true),
            ("Grid", "Grid", true), ("City", "City", true), ("State", "State", true),
            ("Country", "Country", true), ("Qsl", "QSL S/R", true), ("Comment", "Comment", true),
            ("County", "County", false), ("ArrlSection", "ARRL Section", false),
            ("CqZone", "CQ Zone", false), ("ItuZone", "ITU Zone", false),
            ("Continent", "Continent", false), ("SubMode", "Sub-Mode", false),
            ("FreqRx", "Freq Rx", false), ("TxPower", "TX Power", false),
            ("QslVia", "QSL Via", false), ("Lotw", "LoTW S/R", false), ("Notes", "Notes", false),
            ("TimeOff", "Time Off (UTC)", false), ("Station", "Station Callsign", false),
            ("Operator", "Operator", false), ("MyGrid", "My Grid", false),
            ("MyState", "My State", false), ("MyCounty", "My County", false),
        })
        {
            _settings.EnsureLogColumnDefault(key, defaultVisible);
            var option = new ColumnOption(key, displayName, !_settings.HiddenLogColumns.Contains(key));
            option.PropertyChanged += OnColumnOptionChanged;
            ColumnOptions.Add(option);
        }
    }

    public bool IsColumnVisible(string key) =>
        ColumnOptions.FirstOrDefault(c => c.Key == key)?.IsVisible ?? true;

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
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedQso is null) return;
        if (!_dialogService.Confirm($"Delete the QSO with {SelectedQso.Callsign}?")) return;
        await _qsoRepository.DeleteAsync(SelectedQso.Id);
        Qsos.Remove(SelectedQso);
    }
}
