using CommunityToolkit.Mvvm.ComponentModel;

namespace CvarcLogger.App.ViewModels;

/// <summary>One toggleable QSO log grid column, shown as a checkbox in the "Columns..." picker.</summary>
public partial class ColumnOption : ObservableObject
{
    public string Key { get; }
    public string DisplayName { get; }

    [ObservableProperty] private bool isVisible;

    public ColumnOption(string key, string displayName, bool isVisible)
    {
        Key = key;
        DisplayName = displayName;
        this.isVisible = isVisible;
    }
}
