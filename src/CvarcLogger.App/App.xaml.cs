using System.IO;
using System.Windows;
using CvarcLogger.App.Platform;
using CvarcLogger.App.Services;
using CvarcLogger.App.ViewModels;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Awards;
using CvarcLogger.Core.Geo;
using CvarcLogger.Core.Lookup;
using CvarcLogger.Core.Rig;
using CvarcLogger.Data;
using CvarcLogger.Data.Repositories;
using CvarcLogger.Data.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace CvarcLogger.App;

public partial class App : Application
{
    private IHost? _host;
    private IServiceScope? _scope;

    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CVARC Logger");

    /// <summary>Resolves from one long-lived scope created at startup (this is a single-window desktop
    /// app, so a single "session" scope for the whole run is simplest and keeps Scoped services, like
    /// the DbContext and repositories, resolvable without manual scope juggling at every call site).</summary>
    public static IServiceProvider Services =>
        ((App)Current)._scope?.ServiceProvider ?? throw new InvalidOperationException("App scope is not started.");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(Path.Combine(DataDirectory, "backups"));

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(DataDirectory, "logs", "cvarclogger-.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();

        string dbPath = SettingsService.ResolveActiveDatabasePath();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(services =>
            {
                services.AddDbContext<CvarcLoggerDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));

                services.AddSingleton<IClock, SystemClock>();
                services.AddSingleton<ICredentialStore>(_ => new DpapiCredentialStore());
                services.AddSingleton<SettingsService>();

                services.AddScoped<IQsoRepository, QsoRepository>();
                services.AddScoped<IStationProfileRepository, StationProfileRepository>();
                services.AddScoped<IDxccEntityRepository, DxccEntityRepository>();

                services.AddScoped<IAwardsService, AwardsService>();
                services.AddSingleton<ICallsignEntityResolver, CallsignEntityResolver>();
                services.AddSingleton<IGridZoneResolver, GridZoneResolver>();

                services.AddHttpClient<CallookLookupService>();
                services.AddHttpClient<QrzLookupService>();
                services.AddHttpClient<QrzCqLookupService>();
                services.AddScoped<LookupCoordinator>();

                services.AddSingleton<IRigControlService, RigctldClient>();
                services.AddSingleton<RigctldProcessManager>();
                services.AddSingleton<RigControlCoordinator>();
                services.AddSingleton<HamlibRigCatalog>();

                services.AddSingleton<DialogService>();
                services.AddSingleton<FilePickerService>();

                services.AddScoped<MainViewModel>();
                services.AddScoped<QsoEntryViewModel>();
                services.AddScoped<QsoLogViewModel>();
                services.AddScoped<ImportExportViewModel>();
                services.AddTransient<AwardsViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<StationProfileViewModel>();
                services.AddTransient<QsoEditViewModel>();

                services.AddScoped<MainWindow>();
                services.AddTransient<Views.AwardsWindow>();
                services.AddTransient<Views.SettingsWindow>();
                services.AddTransient<Views.StationProfileEditorWindow>();
                services.AddTransient<Views.QsoEditWindow>();
            })
            .Build();

        await _host.StartAsync();

        using (var migrationScope = _host.Services.CreateScope())
        {
            var db = migrationScope.ServiceProvider.GetRequiredService<CvarcLoggerDbContext>();
            await db.Database.MigrateAsync();
            await SeedRunner.SeedIfEmptyAsync(db);
        }

        _scope = _host.Services.CreateScope();
        var mainWindow = _scope.ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        BackupDatabase();

        if (_scope is not null)
        {
            try
            {
                var rig = _scope.ServiceProvider.GetRequiredService<RigControlCoordinator>();
                await rig.DisconnectAsync(); // closes the TCP session and kills rigctld.exe if we launched it
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to disconnect CAT control on exit.");
            }
        }

        _scope?.Dispose();
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private void BackupDatabase()
    {
        try
        {
            string dbPath = SettingsService.ResolveActiveDatabasePath();
            if (!File.Exists(dbPath)) return;

            string backupDir = Path.Combine(DataDirectory, "backups");
            Directory.CreateDirectory(backupDir);

            // Prefix backups with the source DB's own name so switching logs (File > New Log) doesn't
            // mix different logs' backups together under one indistinguishable "cvarclogger-*" name.
            string dbNameStem = Path.GetFileNameWithoutExtension(dbPath);
            string backupPath = Path.Combine(backupDir, $"{dbNameStem}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.bak");
            File.Copy(dbPath, backupPath, overwrite: true);

            var stale = new DirectoryInfo(backupDir).GetFiles($"{dbNameStem}-*.bak")
                .OrderByDescending(f => f.CreationTimeUtc)
                .Skip(20);
            foreach (var file in stale) file.Delete();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to back up database on exit.");
        }
    }
}
