using CommunityToolkit.Maui.Storage;
using CvarcCellLog.Services;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Adif;
using CvarcLogger.Core.Awards;
using CvarcLogger.Core.Models;
using CvarcLogger.Data;
using CvarcLogger.Data.Seed;
using Microsoft.EntityFrameworkCore;

namespace CvarcCellLog.Pages;

/// <summary>Import/Export ADIF and New/Open Log, ported from the WPF app's ImportExportViewModel and
/// MainWindow.xaml.cs, directly in code-behind rather than a separate ViewModel since there's no
/// persistent state to bind -- each action is a one-shot file pick + operation + result alert.</summary>
public partial class FileMenuPage : ContentPage
{
    private readonly IQsoRepository _qsoRepository;
    private readonly ICallsignEntityResolver _entityResolver;
    private readonly CvarcLoggerDbContext _activeDb;
    private readonly IAppRestartService _restartService;

    public FileMenuPage(
        IQsoRepository qsoRepository,
        ICallsignEntityResolver entityResolver,
        CvarcLoggerDbContext activeDb,
        IAppRestartService restartService)
    {
        InitializeComponent();
        _qsoRepository = qsoRepository;
        _entityResolver = entityResolver;
        _activeDb = activeDb;
        _restartService = restartService;
    }

    /// <summary>Ported from the WPF app's "New Log", adapted for Android scoped storage: the WPF version
    /// lets the user pick any folder via SaveFileDialog, but Android only grants SQLite direct raw-file
    /// access inside the app's own private storage -- a SAF-picked destination (even one FileSaver reports
    /// a friendly-looking path for) throws "SQLite Error 14: unable to open database file" the moment EF
    /// Core tries to open it directly, confirmed while building this. So instead of picking a folder, the
    /// user names the new log and it's created under FileSystem.AppDataDirectory, then
    /// create+migrate+seed, best-effort carry the current log's station profiles into it, persist the new
    /// active path, then confirm-and-restart -- MAUI has no way to swap a live DbContext's connection
    /// string mid-session, so every DI-resolved service needs a fresh process to pick up the new file.</summary>
    private async void OnNewLogClicked(object? sender, EventArgs e)
    {
        string? name = await DisplayPromptAsync("New Log", "Name for the new log:",
            initialValue: $"cvarclogger-{DateTime.Now:yyyyMMdd}", maxLength: 60, accept: "Create", cancel: "Cancel");
        if (string.IsNullOrWhiteSpace(name)) return;

        string path = GetUniqueAppDataPath(name);
        try
        {
            var options = new DbContextOptionsBuilder<CvarcLoggerDbContext>().UseSqlite($"Data Source={path}").Options;
            await using var newDb = new CvarcLoggerDbContext(options);
            await newDb.Database.MigrateAsync();
            await SeedRunner.SeedIfEmptyAsync(newDb);
            await CarryStationProfilesAsync(newDb);
        }
        catch (Exception ex)
        {
            await DisplayAlert("New Log", $"Could not set up the new log: {ex.Message}", "OK");
            return;
        }

        DatabasePathService.SetActivePath(path);

        bool restart = await DisplayAlert("New Log",
            $"New log '{Path.GetFileNameWithoutExtension(path)}' created.\n\nCVARC Cell Logger needs to restart to switch to it. Restart now?",
            "Restart", "Later");
        if (restart) _restartService.Restart();
    }

    /// <summary>Best-effort copy of the current log's station profiles into a newly created log, mirroring
    /// the WPF app's CarryStationProfilesAsync -- extended here to also carry Qth/Op/UtcOffsetHours/
    /// ObservesDaylightSavingTime (the WPF version omits those four fields, which looks like an oversight
    /// rather than a deliberate choice; a carried profile silently defaulting to a 0-hour UTC offset would
    /// be a real functional regression, not just a cosmetic gap).</summary>
    private async Task CarryStationProfilesAsync(CvarcLoggerDbContext newDb)
    {
        var profiles = await _activeDb.StationProfiles.AsNoTracking().ToListAsync();
        if (profiles.Count == 0) return;

        foreach (var profile in profiles)
        {
            newDb.StationProfiles.Add(new StationProfile
            {
                Callsign = profile.Callsign,
                OperatorCallsign = profile.OperatorCallsign,
                MyGridSquare = profile.MyGridSquare,
                MyState = profile.MyState,
                MyCounty = profile.MyCounty,
                Qth = profile.Qth,
                Op = profile.Op,
                UtcOffsetHours = profile.UtcOffsetHours,
                ObservesDaylightSavingTime = profile.ObservesDaylightSavingTime,
                IsDefault = profile.IsDefault,
                Notes = profile.Notes,
            });
        }

        await newDb.SaveChangesAsync();
    }

    /// <summary>Ported from the WPF app's "Open Log", adapted for Android scoped storage the same way
    /// OnNewLogClicked is: FilePicker's returned FullPath isn't reliably raw-file-openable by SQLite (it
    /// may point at a SAF document, not a real path), so the picked file is copied into
    /// FileSystem.AppDataDirectory first -- only that copy is validated (same Qsos.Take(1) schema probe as
    /// WPF) and made active, never the original SAF location.</summary>
    private async void OnOpenLogClicked(object? sender, EventArgs e)
    {
        FileResult? file;
        try
        {
            file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select a CVARC Logger database" });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Open Log", $"Could not open the file picker: {ex.Message}", "OK");
            return;
        }
        if (file is null) return;

        if (!file.FileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
        {
            await DisplayAlert("Open Log", "Please select a .db file.", "OK");
            return;
        }

        string path = GetUniqueAppDataPath(Path.GetFileNameWithoutExtension(file.FileName));
        try
        {
            await using (var source = await file.OpenReadAsync())
            await using (var dest = File.Create(path))
            {
                await source.CopyToAsync(dest);
            }

            var options = new DbContextOptionsBuilder<CvarcLoggerDbContext>().UseSqlite($"Data Source={path}").Options;
            await using var testDb = new CvarcLoggerDbContext(options);
            if (!await testDb.Database.CanConnectAsync())
                throw new InvalidOperationException("Unable to connect to the database.");
            await testDb.Qsos.Take(1).ToListAsync();
        }
        catch (Exception ex)
        {
            if (File.Exists(path)) File.Delete(path);
            await DisplayAlert("Open Log", $"'{file.FileName}' doesn't look like a valid CVARC Logger database: {ex.Message}", "OK");
            return;
        }

        DatabasePathService.SetActivePath(path);

        bool restart = await DisplayAlert("Open Log",
            $"Switching to '{file.FileName}'.\n\nCVARC Cell Logger needs to restart. Restart now?",
            "Restart", "Later");
        if (restart) _restartService.Restart();
    }

    /// <summary>Builds a collision-free .db path under FileSystem.AppDataDirectory from a user-supplied or
    /// picked-file name -- shared by New Log and Open Log, both of which must land inside app-private
    /// storage for SQLite to be able to open the file directly (see OnNewLogClicked's doc comment).</summary>
    private static string GetUniqueAppDataPath(string desiredName)
    {
        string safeName = string.Concat(desiredName.Split(Path.GetInvalidFileNameChars())).Trim();
        if (safeName.Length == 0) safeName = "cvarclogger";

        string path = Path.Combine(FileSystem.AppDataDirectory, $"{safeName}.db");
        int suffix = 2;
        while (File.Exists(path))
        {
            path = Path.Combine(FileSystem.AppDataDirectory, $"{safeName} ({suffix}).db");
            suffix++;
        }
        return path;
    }

    private async void OnImportAdifClicked(object? sender, EventArgs e)
    {
        FileResult? file;
        try
        {
            file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select an ADIF file" });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Import ADIF", $"Could not open the file picker: {ex.Message}", "OK");
            return;
        }
        if (file is null) return;

        if (!file.FileName.EndsWith(".adi", StringComparison.OrdinalIgnoreCase)
            && !file.FileName.EndsWith(".adif", StringComparison.OrdinalIgnoreCase))
        {
            await DisplayAlert("Import ADIF", "Please select a .adi or .adif file.", "OK");
            return;
        }

        try
        {
            // Byte-native read, not a StreamReader -- see AdifReader.ReadAll(byte[]) for why: some
            // real-world exporters (confirmed: QRZ Logbook) write non-UTF-8 bytes for accented names.
            var records = AdifReader.ReadAllFromFile(file.FullPath);
            int imported = 0;
            foreach (var record in records)
            {
                var qso = AdifFieldMapper.ToQso(record);
                AdifImportSanitizer.Sanitize(qso);
                if (string.IsNullOrWhiteSpace(qso.Callsign)) continue;

                // Our DxccEntities table is a hand-curated subset (see
                // project_cvarclogger_dxcc_seed_incomplete), so trusting the imported DXCC code verbatim
                // as an FK would throw the moment one record's code isn't in our table. Re-resolve it from
                // the callsign instead, same as a live-logged QSO gets one.
                var resolvedEntity = await _entityResolver.ResolveAsync(qso.Callsign);
                qso.DxccEntityCode = resolvedEntity?.EntityCode;

                await _qsoRepository.AddAsync(qso);
                imported++;
            }

            await DisplayAlert("Import ADIF", $"Imported {imported} of {records.Count} record(s) from {file.FileName}.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Import ADIF", $"Import failed: {ex.Message}", "OK");
        }
    }

    private async void OnExportAdifClicked(object? sender, EventArgs e)
    {
        try
        {
            var qsos = await _qsoRepository.GetAllAsync();

            using var stream = new MemoryStream();
            await using (var writer = new StreamWriter(stream, leaveOpen: true))
            {
                AdifWriter.WriteAll(writer, qsos.Select(AdifFieldMapper.ToAdifRecord));
            }
            stream.Position = 0;

            string fileName = $"cvarclogger-export-{DateTime.Now:yyyyMMdd}.adi";
            var result = await FileSaver.Default.SaveAsync(fileName, stream, CancellationToken.None);

            if (result.IsSuccessful)
            {
                await DisplayAlert("Export ADIF", $"Exported {qsos.Count} QSO(s) to {result.FilePath}.", "OK");
            }
            else if (result.Exception is not null)
            {
                await DisplayAlert("Export ADIF", $"Export failed: {result.Exception.Message}", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Export ADIF", $"Export failed: {ex.Message}", "OK");
        }
    }
}
