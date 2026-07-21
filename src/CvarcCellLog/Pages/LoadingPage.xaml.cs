using CvarcLogger.Data;
using CvarcLogger.Data.Seed;
using Microsoft.EntityFrameworkCore;

namespace CvarcCellLog.Pages;

public partial class LoadingPage : ContentPage
{
    private readonly CvarcLoggerDbContext _db;
    private readonly AppShell _appShell;

    public LoadingPage(CvarcLoggerDbContext db, AppShell appShell)
    {
        InitializeComponent();
        _db = db;
        _appShell = appShell;
        VersionLabel.Text = $"v{AppInfo.Current.VersionString}";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Runs off the UI thread so the Android startup ANR watchdog doesn't trip during migration/seed.
        await Task.Run(async () =>
        {
            await _db.Database.MigrateAsync();
            await SeedRunner.SeedIfEmptyAsync(_db);
        });

        Application.Current!.Windows[0].Page = _appShell;
    }
}
