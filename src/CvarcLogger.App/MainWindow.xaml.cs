using System.Diagnostics;
using System.IO;
using System.Windows;
using CvarcLogger.App.Services;
using CvarcLogger.App.ViewModels;
using CvarcLogger.App.Views;
using CvarcLogger.Core.Models;
using CvarcLogger.Data;
using CvarcLogger.Data.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CvarcLogger.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Title = $"CVARC Logger v{AppVersion.Current}";

        string dbPath = SettingsService.ResolveActiveDatabasePath();
        LogNameText.Text = $"Log: {Path.GetFileName(dbPath)}";
        LogNameText.ToolTip = dbPath;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
        CheckHamlibAvailability();
    }

    private void CheckHamlibAvailability()
    {
        if (!SettingsService.IsHamlibAvailable())
        {
            var dialogService = App.Services.GetRequiredService<DialogService>();
            bool wantToDownload = dialogService.Confirm(
                "Hamlib (radio control library) is not installed on this computer.\n\n" +
                "Hamlib is required for CAT (Computer-Aided Transceiver) control of serial radios. " +
                "CVARC Logger will still work for manual logging without it, but CAT control will be unavailable.\n\n" +
                "Would you like to visit the Hamlib download page to install it?");

            if (wantToDownload)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://hamlib.github.io/",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    dialogService.ShowError($"Could not open the Hamlib download page: {ex.Message}");
                }
            }
        }
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Quick "grab the current log" backup -- copies the active .db file next to the .exe as
    /// backup-yyyyMMdd.db. Same one-shot intent as CvarcCellLog's Save Log button (writes backup.db to
    /// Downloads), just dated instead of a fixed name since a desktop backup folder can accumulate
    /// several without needing MediaStore-style overwrite-in-place handling -- running it twice in the
    /// same day still overwrites that day's copy via File.Copy's overwrite flag.</summary>
    private void SaveLogMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string sourcePath = SettingsService.ResolveActiveDatabasePath();
            string destPath = Path.Combine(AppContext.BaseDirectory, $"backup-{DateTime.Now:yyyyMMdd}.db");
            File.Copy(sourcePath, destPath, overwrite: true);
            MessageBox.Show(this, $"Saved a copy of the current log to {Path.GetFileName(destPath)}.",
                "Save Log", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not save the backup: {ex.Message}",
                "Save Log", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AwardsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var window = App.Services.GetRequiredService<AwardsWindow>();
        window.Owner = this;
        window.Show();
    }

    private void LookupMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var window = App.Services.GetRequiredService<LookupSettingsWindow>();
        window.Owner = this;
        window.ShowDialog();
    }

    private void CatControlMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var window = App.Services.GetRequiredService<CatControlWindow>();
        window.Owner = this;
        window.ShowDialog();
    }

    private void StationProfilesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var window = App.Services.GetRequiredService<StationProfileEditorWindow>();
        window.Owner = this;
        window.ShowDialog();
        _ = _viewModel.QsoEntry.InitializeAsync();
    }

    private async void NewLogMenuItem_Click(object sender, RoutedEventArgs e)
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
            dialogService.ShowError($"Could not create the new log: {ex.Message}");
            return;
        }

        await CarryStationProfilesAsync(path);

        settings.CurrentDatabasePath = path;

        if (!dialogService.Confirm($"New log created at:\n{path}\n\nCVARC Logger needs to restart to switch to it. Restart now?"))
        {
            dialogService.ShowInfo("The new log is ready — restart CVARC Logger when you're ready to switch to it.");
            return;
        }

        RestartApplication();
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

    private async void OpenLogMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var filePicker = App.Services.GetRequiredService<FilePickerService>();
        var dialogService = App.Services.GetRequiredService<DialogService>();
        var settings = App.Services.GetRequiredService<SettingsService>();

        string? path = filePicker.PickExistingDatabaseFileToOpen();
        if (path is null) return;

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
            dialogService.ShowError($"'{path}' doesn't look like a valid CVARC Logger database: {ex.Message}");
            return;
        }

        settings.CurrentDatabasePath = path;

        if (!dialogService.Confirm($"Switching to:\n{path}\n\nCVARC Logger needs to restart. Restart now?"))
        {
            dialogService.ShowInfo("Log switched — restart CVARC Logger when you're ready.");
            return;
        }

        RestartApplication();
    }

    private static void RestartApplication()
    {
        string exePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "CvarcLogger.exe");
        var psi = new ProcessStartInfo { UseShellExecute = true };

        if (Path.GetFileNameWithoutExtension(exePath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            // Running via `dotnet run` — relaunch through dotnet against our own assembly rather than
            // starting a bare "dotnet" with no arguments.
            psi.FileName = "dotnet";
            psi.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "CvarcLogger.dll"));
        }
        else
        {
            psi.FileName = exePath;
        }

        Process.Start(psi);
        Application.Current.Shutdown();
    }
}
