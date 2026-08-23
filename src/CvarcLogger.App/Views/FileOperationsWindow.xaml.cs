using System.IO;
using System.Windows;
using CvarcLogger.App.Services;
using CvarcLogger.App.ViewModels;
using CvarcLogger.Core.Models;
using CvarcLogger.Data;
using CvarcLogger.Data.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CvarcLogger.App.Views;

public partial class FileOperationsWindow : Window
{
    public FileOperationsWindow()
    {
        InitializeComponent();
    }

    /// <summary>Shows the modal busy overlay with a given message. Also flips the cursor to Wait so
    /// clicks outside the overlay still feel "busy" even though the overlay blocks them anyway.</summary>
    private void ShowBusy(string message)
    {
        BusyMessage.Text = message;
        BusyOverlay.Visibility = Visibility.Visible;
        Cursor = System.Windows.Input.Cursors.Wait;
    }

    private void HideBusy()
    {
        BusyOverlay.Visibility = Visibility.Collapsed;
        Cursor = null;
    }

    private async void NewLog_Click(object sender, RoutedEventArgs e)
    {
        var filePicker = App.Services.GetRequiredService<FilePickerService>();
        var dialogService = App.Services.GetRequiredService<DialogService>();
        var settings = App.Services.GetRequiredService<SettingsService>();

        string suggestedName = $"cvarclogger-{DateTime.Now:yyyyMMdd}.db";
        string? path = filePicker.PickNewDatabaseFileToCreate(suggestedName);
        if (path is null) return;

        if (File.Exists(path))
        {
            // SaveFileDialog already confirmed overwrite with the user; delete rather than reuse so
            // migrations run against a genuinely empty file instead of some unrelated leftover data.
            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                dialogService.ShowError($"Could not replace the existing file: {ex.Message}");
                return;
            }
        }

        ShowBusy("Creating new log...");
        try
        {
            var options = new DbContextOptionsBuilder<CvarcLoggerDbContext>()
                .UseSqlite($"Data Source={path}")
                .Options;
            await using var db = new CvarcLoggerDbContext(options);
            await db.Database.MigrateAsync();
            await SeedRunner.SeedIfEmptyAsync(db);
        }
        catch (Exception ex)
        {
            HideBusy();
            dialogService.ShowError($"Could not create the new log: {ex.Message}");
            return;
        }

        await CarryStationProfilesAsync(path);
        HideBusy();

        settings.CurrentDatabasePath = path;

        if (!dialogService.Confirm($"New log created at:\n{path}\n\nCVARC Logger needs to restart to switch to it. Restart now?"))
        {
            dialogService.ShowInfo("The new log is ready — restart CVARC Logger when you're ready to switch to it.");
            return;
        }

        AppRestarter.Restart();
    }

    /// <summary>Copies station profiles from the currently active database into a freshly created one,
    /// so switching to a new log doesn't mean re-entering your callsign/grid/etc. Best-effort — a
    /// failure here shouldn't block the new log from being usable.</summary>
    private async Task CarryStationProfilesAsync(string newDbPath)
    {
        try
        {
            var currentDb = App.Services.GetRequiredService<CvarcLoggerDbContext>();
            var currentProfiles = await currentDb.StationProfiles.AsNoTracking().ToListAsync();
            if (currentProfiles.Count == 0) return;

            var newOptions = new DbContextOptionsBuilder<CvarcLoggerDbContext>()
                .UseSqlite($"Data Source={newDbPath}")
                .Options;
            await using var newDb = new CvarcLoggerDbContext(newOptions);
            foreach (var profile in currentProfiles)
            {
                newDb.StationProfiles.Add(new StationProfile
                {
                    Callsign = profile.Callsign,
                    OperatorCallsign = profile.OperatorCallsign,
                    MyGridSquare = profile.MyGridSquare,
                    MyState = profile.MyState,
                    MyCounty = profile.MyCounty,
                    IsDefault = profile.IsDefault,
                    Notes = profile.Notes,
                });
            }
            await newDb.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            App.Services.GetRequiredService<DialogService>()
                .ShowError($"The new log was created, but carrying over station profiles failed: {ex.Message}");
        }
    }

    private async void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        var filePicker = App.Services.GetRequiredService<FilePickerService>();
        var dialogService = App.Services.GetRequiredService<DialogService>();
        var settings = App.Services.GetRequiredService<SettingsService>();

        string? path = filePicker.PickExistingDatabaseFileToOpen();
        if (path is null) return;

        ShowBusy("Opening log...");
        try
        {
            var options = new DbContextOptionsBuilder<CvarcLoggerDbContext>()
                .UseSqlite($"Data Source={path}")
                .Options;
            await using var db = new CvarcLoggerDbContext(options);
            if (!await db.Database.CanConnectAsync())
                throw new InvalidOperationException("Could not open the database file.");
            await db.Qsos.Take(1).ToListAsync(); // confirms it's actually a CVARC Logger-schema database
        }
        catch (Exception ex)
        {
            HideBusy();
            dialogService.ShowError($"'{path}' doesn't look like a valid CVARC Logger database: {ex.Message}");
            return;
        }
        HideBusy();

        settings.CurrentDatabasePath = path;

        if (!dialogService.Confirm($"Switching to:\n{path}\n\nCVARC Logger needs to restart. Restart now?"))
        {
            dialogService.ShowInfo("Log switched — restart CVARC Logger when you're ready.");
            return;
        }

        AppRestarter.Restart();
    }

    /// <summary>Saves a named copy of the active .db file wherever the operator chooses -- lets separate
    /// events (Field Day, a SOTA activation, etc.) get their own named log file instead of every backup
    /// landing under one auto-dated name. Suggests backup-yyyyMMdd.db as a starting point/default name.</summary>
    private async void SaveLog_Click(object sender, RoutedEventArgs e)
    {
        var filePicker = App.Services.GetRequiredService<FilePickerService>();

        string suggestedName = $"backup-{DateTime.Now:yyyyMMdd}.db";
        string? destPath = filePicker.PickBackupDatabaseFileToSave(suggestedName);
        if (destPath is null) return;

        ShowBusy("Saving log...");
        try
        {
            string sourcePath = SettingsService.ResolveActiveDatabasePath();
            // File.Copy runs on the UI thread otherwise; move to background so the overlay actually paints.
            await Task.Run(() => File.Copy(sourcePath, destPath, overwrite: true));
            HideBusy();
            MessageBox.Show(this, $"Saved a copy of the current log to {Path.GetFileName(destPath)}.",
                "Save Log", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            HideBusy();
            MessageBox.Show(this, $"Could not save the backup: {ex.Message}",
                "Save Log", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Wraps an import/export command with the busy overlay and live progress subscription.</summary>
    private async Task RunWithProgress(string initialMessage, Func<ImportExportViewModel, Task> action)
    {
        var importExport = App.Services.GetRequiredService<ImportExportViewModel>();
        ShowBusy(initialMessage);
        void OnProgress(object? _, string msg) => Dispatcher.BeginInvoke(() => BusyMessage.Text = msg);
        importExport.ProgressChanged += OnProgress;
        try
        {
            await action(importExport);
        }
        finally
        {
            importExport.ProgressChanged -= OnProgress;
            HideBusy();
        }
    }

    private async void ImportADIF_Click(object sender, RoutedEventArgs e) =>
        await RunWithProgress("Importing ADIF...", vm => vm.ImportCommand.ExecuteAsync(null));

    private async void ExportADIF_Click(object sender, RoutedEventArgs e) =>
        await RunWithProgress("Exporting ADIF...", vm => vm.ExportCommand.ExecuteAsync(null));

    private async void ImportCabrillo_Click(object sender, RoutedEventArgs e) =>
        await RunWithProgress("Importing Cabrillo...", vm => vm.ImportCabrilloCommand.ExecuteAsync(null));

    private async void ExportCabrillo_Click(object sender, RoutedEventArgs e)
    {
        // Prompt for contest info first -- callsign + contest name are required per the Cabrillo spec.
        // Defaults come from the operator's default station profile, then get replaced by any previously-
        // saved submission for the same contest once the user picks one from the Contest dropdown.
        var stationRepo = App.Services.GetRequiredService<CvarcLogger.Core.Abstractions.IStationProfileRepository>();
        var importExport = App.Services.GetRequiredService<ImportExportViewModel>();

        var defaultProfile = (await stationRepo.GetAllAsync()).FirstOrDefault(p => p.IsDefault)
            ?? (await stationRepo.GetAllAsync()).FirstOrDefault();

        var defaults = new CvarcLogger.Core.Cabrillo.CabrilloContestInfo
        {
            Callsign = defaultProfile?.Callsign ?? string.Empty,
            Location = defaultProfile?.MyState ?? string.Empty,
        };

        // The dialog itself asks us for a previously-saved header whenever the operator changes the
        // Contest field -- so a repeat export of ARRL-DX-CW auto-restores last year's address block.
        var dialog = new CabrilloExportDialog(defaults, importExport.GetLatestSubmissionAsync) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        await RunWithProgress("Exporting Cabrillo...", vm => vm.ExportCabrilloCommand.ExecuteAsync(dialog.Result));
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
