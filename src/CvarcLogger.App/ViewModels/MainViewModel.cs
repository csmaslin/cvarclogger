using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CvarcLogger.App.Services;
using CvarcLogger.Core.UiStandards;

namespace CvarcLogger.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly WsjtxUdpListenerService _wsjtxListener;

    public QsoEntryViewModel QsoEntry { get; }
    public QsoLogViewModel QsoLog { get; }
    public ImportExportViewModel ImportExport { get; }

    public MainViewModel(QsoEntryViewModel qsoEntry, QsoLogViewModel qsoLog, ImportExportViewModel importExport, WsjtxUdpListenerService wsjtxListener)
    {
        QsoEntry = qsoEntry;
        QsoLog = qsoLog;
        ImportExport = importExport;
        _wsjtxListener = wsjtxListener;

        QsoEntry.QsoLogged += async (_, _) => await QsoLog.RefreshAsync();
        QsoEntry.CallsignChanged += (_, callsign) => QsoLog.SearchText = callsign;
        ImportExport.ImportCompleted += async (_, _) => await QsoLog.RefreshAsync();
        _wsjtxListener.QsoLogged += async (_, _) => await QsoLog.RefreshAsync();
        QsoLog.ColumnVisibilityChanged += (_, _) => QsoEntry.NotifyFieldVisibilityChanged();
        QsoLog.ModeLabelsChanged += (_, _) => QsoEntry.NotifyModeLabelsChanged();

        // Picking a different tab in the Column Visibility picker switches the app's actual live mode
        // to match (sidebar highlight included) -- full two-way sync with the sidebar's mode buttons,
        // per the operator's explicit request.
        QsoLog.PickerModeTabChanged += (_, mode) => QsoEntry.SelectedEntryModeOption = QsoEntryModeOptions.For(mode);

        // Grid columns and entry-form fields now share one per-mode visibility set (SettingsService.
        // GetHiddenColumns), so switching Log Entry Mode has to push the new mode into QsoLog even though
        // no column checkbox was actually touched -- QsoLog.IsColumnVisible only reads whatever mode
        // SetLiveMode last told it about, it doesn't watch QsoEntry itself. This also keeps the picker's
        // selected tab following the live mode continuously (not just at the moment the picker is opened),
        // so a sidebar click while the picker is already open moves its tab too. Safe against feedback
        // with PickerModeTabChanged above: both QsoEntryModeOption and ColumnPickerModeTab lookups here
        // resolve to the SAME already-selected instance once in sync, so the generated property setters'
        // equality checks make the second leg of any round trip a no-op.
        QsoEntry.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(QsoEntryViewModel.SelectedEntryModeOption))
            {
                QsoLog.SetLiveMode(QsoEntry.SelectedEntryModeOption.Value.ToString());

                var matchingTab = QsoLog.PickerModeTabs.FirstOrDefault(t => t.Value == QsoEntry.SelectedEntryModeOption.Value);
                if (matchingTab is not null) QsoLog.SelectedPickerModeTab = matchingTab;
            }
        };
        QsoLog.SetLiveMode(QsoEntry.SelectedEntryModeOption.Value.ToString());
    }

    public async Task InitializeAsync()
    {
        await QsoEntry.InitializeAsync();
        await QsoLog.RefreshAsync();
        _wsjtxListener.ApplyEnabledState();
    }

    public void SwitchMode(string modeName)
    {
        var mode = modeName switch
        {
            "Normal" => QsoEntryMode.Normal,
            "Contest" => QsoEntryMode.Contest,
            "SOTA" => QsoEntryMode.Sota,
            "POTA" => QsoEntryMode.Pota,
            "Net" => QsoEntryMode.Net,
            "Custom1" => QsoEntryMode.Custom1,
            "Custom2" => QsoEntryMode.Custom2,
            "Custom3" => QsoEntryMode.Custom3,
            "All" => QsoEntryMode.All,
            _ => QsoEntryMode.Normal
        };

        QsoEntry.SelectedEntryModeOption = QsoEntryModeOptions.For(mode);
    }
}
