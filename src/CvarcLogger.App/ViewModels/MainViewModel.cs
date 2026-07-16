using CommunityToolkit.Mvvm.ComponentModel;

namespace CvarcLogger.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public QsoEntryViewModel QsoEntry { get; }
    public QsoLogViewModel QsoLog { get; }
    public ImportExportViewModel ImportExport { get; }

    public MainViewModel(QsoEntryViewModel qsoEntry, QsoLogViewModel qsoLog, ImportExportViewModel importExport)
    {
        QsoEntry = qsoEntry;
        QsoLog = qsoLog;
        ImportExport = importExport;

        QsoEntry.QsoLogged += async (_, _) => await QsoLog.RefreshAsync();
        QsoEntry.CallsignChanged += (_, callsign) => QsoLog.SearchText = callsign;
        ImportExport.ImportCompleted += async (_, _) => await QsoLog.RefreshAsync();
    }

    public async Task InitializeAsync()
    {
        await QsoEntry.InitializeAsync();
        await QsoLog.RefreshAsync();
    }
}
