using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvarcCellLog.Models;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Models;

namespace CvarcCellLog.ViewModels;

public partial class QsoLogViewModel : ObservableObject
{
    private readonly IQsoRepository _qsoRepository;
    private List<Qso> _allQsos = new();

    [ObservableProperty] private string? searchText;
    [ObservableProperty] private Qso? selectedQso;
    [ObservableProperty] private string emptyStateMessage = "No QSOs yet.";
    [ObservableProperty] private LogColumnKey? sortColumn;
    [ObservableProperty] private bool sortAscending = true;

    public ObservableCollection<Qso> Qsos { get; } = new();

    public QsoLogViewModel(IQsoRepository qsoRepository)
    {
        _qsoRepository = qsoRepository;
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        _allQsos = await _qsoRepository.GetAllAsync();
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string? value) => ApplyFilter();

    /// <summary>Tapping a QSO Log column header calls this (see QsoLogPage.xaml.cs's dynamically-built
    /// header row) -- tapping the already-sorted column flips direction, tapping a different one sorts
    /// ascending on it.</summary>
    public void SetSort(LogColumnKey key)
    {
        if (SortColumn == key) SortAscending = !SortAscending;
        else
        {
            SortColumn = key;
            SortAscending = true;
        }
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filtered = (string.IsNullOrWhiteSpace(SearchText) ? _allQsos : _allQsos.Where(MatchesSearch)).ToList();
        if (SortColumn.HasValue) filtered.Sort(new QsoColumnComparer(SortColumn.Value, SortAscending));

        Qsos.Clear();
        foreach (var qso in filtered) Qsos.Add(qso);

        // Distinguish "nothing logged yet" from "this search matched nothing" -- both look identical
        // as an empty Qsos collection otherwise.
        EmptyStateMessage = _allQsos.Count == 0
            ? "No QSOs yet."
            : $"No matches for \"{SearchText}\".";
    }

    private bool MatchesSearch(Qso qso) =>
        qso.Callsign.Contains(SearchText!, StringComparison.OrdinalIgnoreCase)
        || (qso.Name?.Contains(SearchText!, StringComparison.OrdinalIgnoreCase) ?? false)
        || (qso.GridSquare?.Contains(SearchText!, StringComparison.OrdinalIgnoreCase) ?? false)
        || (qso.City?.Contains(SearchText!, StringComparison.OrdinalIgnoreCase) ?? false);

    [RelayCommand]
    private async Task DeleteAsync(Qso? qso)
    {
        if (qso is null) return;

        await _qsoRepository.DeleteAsync(qso.Id);
        _allQsos.Remove(qso);
        Qsos.Remove(qso);
    }

    [RelayCommand]
    private static async Task EditAsync(Qso? qso)
    {
        if (qso is null) return;
        await Shell.Current.GoToAsync($"QsoEditPage?id={qso.Id}");
    }

    /// <summary>Sorts by whatever raw value LogColumns.GetSortKey returns for the active sort column --
    /// deliberately not the formatted display string (LogColumns.GetValue), since e.g. sorting
    /// Date/Time or Frequency as text would order "10" before "2" and dates out of chronological order.
    /// Both sides of any one comparison always come from the same column, so they're always the same
    /// underlying type, which is all Comparer&lt;object&gt;.Default needs to dispatch to the right
    /// IComparable.CompareTo overload.</summary>
    private sealed class QsoColumnComparer : IComparer<Qso>
    {
        private readonly LogColumnKey _key;
        private readonly int _direction;

        public QsoColumnComparer(LogColumnKey key, bool ascending)
        {
            _key = key;
            _direction = ascending ? 1 : -1;
        }

        public int Compare(Qso? x, Qso? y)
        {
            var a = LogColumns.GetSortKey(x!, _key);
            var b = LogColumns.GetSortKey(y!, _key);
            if (a is null && b is null) return 0;
            if (a is null) return -_direction;
            if (b is null) return _direction;
            return _direction * Comparer<object>.Default.Compare(a, b);
        }
    }
}
