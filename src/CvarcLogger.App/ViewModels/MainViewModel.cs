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

        // Grid columns and entry-form fields now share one per-mode visibility set (SettingsService.
        // GetHiddenColumns), so switching Log Entry Mode has to push the new mode into QsoLog even though
        // no column checkbox was actually touched -- QsoLog.IsColumnVisible only reads whatever mode
        // SetLiveMode last told it about, it doesn't watch QsoEntry itself.
        QsoEntry.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(QsoEntryViewModel.SelectedEntryModeOption))
                QsoLog.SetLiveMode(QsoEntry.SelectedEntryModeOption.Value.ToString());
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
            "All" => QsoEntryMode.All,
            _ => QsoEntryMode.Normal
        };

        QsoEntry.SelectedEntryModeOption = QsoEntryModeOptions.For(mode);
    }
}
