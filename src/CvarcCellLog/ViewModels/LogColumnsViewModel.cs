using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvarcCellLog.Models;
using CvarcCellLog.Services;

namespace CvarcCellLog.ViewModels;

/// <summary>One row in the Columns picker: a field the QSO Log can show, whether it's currently
/// visible, and its position (order in this list = left-to-right column order once saved).</summary>
public partial class LogColumnItemViewModel : ObservableObject
{
    public LogColumnKey Key { get; }
    public string Header { get; }

    [ObservableProperty] private bool isVisible;

    public LogColumnItemViewModel(LogColumnKey key, string header, bool isVisible)
    {
        Key = key;
        Header = header;
        this.isVisible = isVisible;
    }
}

/// <summary>Backs the "Columns" screen reachable from the QSO Log's toolbar -- lets the operator
/// choose which fields show as columns and reorder them (Up/Down rather than drag-and-drop, which
/// needs no gesture-threshold tuning and works reliably on any touch screen).</summary>
public partial class LogColumnsViewModel : ObservableObject
{
    public ObservableCollection<LogColumnItemViewModel> Items { get; } = new();

    public LogColumnsViewModel()
    {
        var activeOrder = LogColumnPreferences.Load();
        var orderedDefinitions = activeOrder
            .Select(LogColumns.Get)
            .Concat(LogColumns.All.Where(c => !activeOrder.Contains(c.Key)));

        foreach (var column in orderedDefinitions)
            Items.Add(new LogColumnItemViewModel(column.Key, column.Header, activeOrder.Contains(column.Key)));
    }

    [RelayCommand]
    private void MoveUp(LogColumnItemViewModel? item)
    {
        if (item is null) return;
        int index = Items.IndexOf(item);
        if (index > 0) Items.Move(index, index - 1);
    }

    [RelayCommand]
    private void MoveDown(LogColumnItemViewModel? item)
    {
        if (item is null) return;
        int index = Items.IndexOf(item);
        if (index >= 0 && index < Items.Count - 1) Items.Move(index, index + 1);
    }

    /// <summary>Persists the current checked/ordered state. Falls back to Callsign-only rather than
    /// saving an empty column list -- an empty QSO Log table would look broken, not "no columns".</summary>
    public void Save()
    {
        var visibleKeys = Items.Where(i => i.IsVisible).Select(i => i.Key).ToList();
        if (visibleKeys.Count == 0) visibleKeys.Add(LogColumnKey.Callsign);
        LogColumnPreferences.Save(visibleKeys);
    }
}
