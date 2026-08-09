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
