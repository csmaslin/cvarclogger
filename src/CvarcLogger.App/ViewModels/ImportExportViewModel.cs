using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvarcLogger.App.Services;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Adif;
using CvarcLogger.Core.Awards;

namespace CvarcLogger.App.ViewModels;

public partial class ImportExportViewModel : ObservableObject
{
    private readonly IQsoRepository _qsoRepository;
    private readonly ICallsignEntityResolver _entityResolver;
    private readonly FilePickerService _filePicker;
    private readonly DialogService _dialogService;

    public event EventHandler? ImportCompleted;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? lastResultMessage;

    public ImportExportViewModel(
        IQsoRepository qsoRepository,
        ICallsignEntityResolver entityResolver,
        FilePickerService filePicker,
        DialogService dialogService)
    {
        _qsoRepository = qsoRepository;
        _entityResolver = entityResolver;
        _filePicker = filePicker;
        _dialogService = dialogService;
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var path = _filePicker.PickAdifFileToOpen();
        if (path is null) return;

        IsBusy = true;
        try
        {
            // Reads raw bytes rather than going through StreamReader (which would decode the whole file
            // as UTF-8 up front) -- see AdifReader.ReadAll(byte[]) for why: some real-world exporters
            // (confirmed: QRZ Logbook) write non-UTF-8 bytes for accented names, and only byte-native
            // reading can recover them instead of corrupting them into U+FFFD.
            var records = AdifReader.ReadAllFromFile(path);
            int imported = 0;
            foreach (var record in records)
            {
                var qso = AdifFieldMapper.ToQso(record);
                if (string.IsNullOrWhiteSpace(qso.Callsign)) continue;

                // The imported DXCC code is whatever the source software assigned. Our own DxccEntities
                // table is a hand-curated subset, so trusting that raw code verbatim as an FK reference
                // would throw the moment one record's code isn't in our table -- aborting the entire
                // import partway through. Re-resolve it from the callsign instead, the same way a
                // live-logged QSO gets one, so it always references a row that actually exists and stays
                // consistent with how the rest of the app (Awards, etc.) computes DXCC entities.
                var resolvedEntity = await _entityResolver.ResolveAsync(qso.Callsign);
                qso.DxccEntityCode = resolvedEntity?.EntityCode;

                await _qsoRepository.AddAsync(qso);
                imported++;
            }

            LastResultMessage = $"Imported {imported} of {records.Count} record(s) from {Path.GetFileName(path)}.";
            _dialogService.ShowInfo(LastResultMessage);
            ImportCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Import failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        // Default the export filename to the active log's database name plus a date/time stamp, e.g.
        // "FieldDay2026_20260722_153045.adi", so exports from different logs (and repeated exports of the
        // same one) are self-identifying and don't overwrite each other.
        string dbName = Path.GetFileNameWithoutExtension(SettingsService.ResolveActiveDatabasePath());
        var path = _filePicker.PickAdifFileToSave($"{dbName}_{DateTime.Now:yyyyMMdd_HHmmss}.adi");
        if (path is null) return;

        IsBusy = true;
        try
        {
            var qsos = await _qsoRepository.GetAllAsync();
            using var writer = new StreamWriter(path, append: false);
            AdifWriter.WriteAll(writer, qsos.Select(AdifFieldMapper.ToAdifRecord));
            LastResultMessage = $"Exported {qsos.Count} QSO(s) to {Path.GetFileName(path)}.";
            _dialogService.ShowInfo(LastResultMessage);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Export failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
