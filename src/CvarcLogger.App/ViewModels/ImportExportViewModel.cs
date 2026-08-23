using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvarcLogger.App.Services;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Adif;
using CvarcLogger.Core.Awards;
using CvarcLogger.Core.Cabrillo;

namespace CvarcLogger.App.ViewModels;

public partial class ImportExportViewModel : ObservableObject
{
    private readonly IQsoRepository _qsoRepository;
    private readonly ICallsignEntityResolver _entityResolver;
    private readonly FilePickerService _filePicker;
    private readonly DialogService _dialogService;

    public event EventHandler? ImportCompleted;
    public event EventHandler<string>? ProgressChanged;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? lastResultMessage;

    private void ReportProgress(string message) => ProgressChanged?.Invoke(this, message);

    /// <summary>Strip characters Windows won't allow in a filename plus whitespace, so callsigns/contest
    /// names go straight into a suggested Save-As name without producing an invalid path.</summary>
    private static string SanitizeForFilename(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var invalid = Path.GetInvalidFileNameChars().Concat(new[] { ' ' }).ToHashSet();
        return new string(value.Where(c => !invalid.Contains(c)).ToArray());
    }

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
            ReportProgress("Reading file...");
            var records = AdifReader.ReadAllFromFile(path);
            int imported = 0;
            int total = records.Count;
            ReportProgress($"Importing 0 of {total} record(s)...");
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
                if (imported % 25 == 0 || imported == total)
                    ReportProgress($"Importing {imported} of {total} record(s)...");
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
            ReportProgress("Loading QSOs...");
            var qsos = await _qsoRepository.GetAllAsync();
            ReportProgress($"Writing {qsos.Count} QSO(s)...");
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

    [RelayCommand]
    private async Task ImportCabrilloAsync()
    {
        var path = _filePicker.PickCabrilloFileToOpen();
        if (path is null) return;

        IsBusy = true;
        try
        {
            ReportProgress("Reading file...");
            var result = CabrilloReader.ReadAll(path);
            int imported = 0;
            int total = result.Qsos.Count;
            ReportProgress($"Importing 0 of {total} QSO(s)...");
            foreach (var qso in result.Qsos)
            {
                if (string.IsNullOrWhiteSpace(qso.Callsign)) continue;

                // Carry the contest header into each QSO so contest reports later can find them.
                if (!string.IsNullOrWhiteSpace(result.Info.Contest))
                    qso.ContestId = result.Info.Contest;

                var resolvedEntity = await _entityResolver.ResolveAsync(qso.Callsign);
                qso.DxccEntityCode = resolvedEntity?.EntityCode;

                await _qsoRepository.AddAsync(qso);
                imported++;
                if (imported % 25 == 0 || imported == total)
                    ReportProgress($"Importing {imported} of {total} QSO(s)...");
            }

            LastResultMessage = $"Imported {imported} of {result.Qsos.Count} QSO(s) from Cabrillo file {Path.GetFileName(path)}.";
            _dialogService.ShowInfo(LastResultMessage);
            ImportCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Cabrillo import failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportCabrilloAsync(CabrilloContestInfo? info)
    {
        // Contest info is supplied by the Cabrillo Export dialog; callers that don't supply one just
        // get a minimal header with only the callsign inferred from the active station profile.
        info ??= new CabrilloContestInfo();

        // Filename: <contest>_<callsign>_<date>.cbr for easy recognition (e.g. "ARRL-DX-CW_AA6CV_20260823.cbr").
        // Falls back to the DB name when contest/callsign are blank so the file is still self-identifying.
        string dbName = Path.GetFileNameWithoutExtension(SettingsService.ResolveActiveDatabasePath());
        string contestPart = SanitizeForFilename(info.Contest);
        string callPart = SanitizeForFilename(info.Callsign);
        string suggestedName = (!string.IsNullOrEmpty(contestPart), !string.IsNullOrEmpty(callPart)) switch
        {
            (true, true) => $"{contestPart}_{callPart}_{DateTime.Now:yyyyMMdd}.cbr",
            (true, false) => $"{contestPart}_{DateTime.Now:yyyyMMdd}.cbr",
            (false, true) => $"{callPart}_{DateTime.Now:yyyyMMdd_HHmmss}.cbr",
            _ => $"{dbName}_{DateTime.Now:yyyyMMdd_HHmmss}.cbr",
        };
        var path = _filePicker.PickCabrilloFileToSave(suggestedName);
        if (path is null) return;

        IsBusy = true;
        try
        {
            ReportProgress("Loading QSOs...");
            var qsos = await _qsoRepository.GetAllAsync();
            ReportProgress($"Writing {qsos.Count} QSO(s) as Cabrillo...");
            using var writer = new StreamWriter(path, append: false);
            CabrilloWriter.WriteAll(writer, info, qsos, info.Callsign);
            LastResultMessage = $"Exported {qsos.Count} QSO(s) to Cabrillo file {Path.GetFileName(path)}.";
            _dialogService.ShowInfo(LastResultMessage);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Cabrillo export failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
