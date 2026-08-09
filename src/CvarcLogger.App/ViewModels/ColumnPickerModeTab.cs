using CommunityToolkit.Mvvm.ComponentModel;
using CvarcLogger.Core.UiStandards;

namespace CvarcLogger.App.ViewModels;

/// <summary>One Log Entry Mode's tab in the Column Visibility picker (see QsoLogViewModel.PickerModeTabs).
/// Distinct from QsoEntryModeOption -- that one's display text is fixed (used for the sidebar mode
/// buttons, which don't get renamed), whereas Label here is user-editable per SettingsService.
/// GetModeTabLabel/SetModeTabLabel. Net is deliberately excluded from the picker's tab list (see
/// QsoLogViewModel's constructor) since it isn't wired into any UI yet.</summary>
public partial class ColumnPickerModeTab : ObservableObject
{
    public QsoEntryMode Value { get; }

    /// <summary>False only for "All" -- left as a static catch-all tab per the user's explicit choice, so
    /// only the 4 activity-specific tabs (Normal/Contest/SOTA/POTA) need to be tracked/renamed.</summary>
    public bool IsRenameable { get; }

    [ObservableProperty] private string label;

    public ColumnPickerModeTab(QsoEntryMode value, string label, bool isRenameable)
    {
        Value = value;
        this.label = label;
        IsRenameable = isRenameable;
    }
}
