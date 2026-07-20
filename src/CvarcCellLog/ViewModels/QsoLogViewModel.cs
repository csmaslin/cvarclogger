using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Models;

namespace CvarcCellLog.ViewModels;

public partial class QsoLogViewModel : ObservableObject
{
    private readonly IQsoRepository _qsoRepository;
    private List<Qso> _allQsos = new();

    [ObservableProperty] private string? searchText;
    [ObservableProperty] private Qso? selectedQso;

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

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allQsos
            : _allQsos.Where(MatchesSearch).ToList();

        Qsos.Clear();
        foreach (var qso in filtered) Qsos.Add(qso);
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
}
