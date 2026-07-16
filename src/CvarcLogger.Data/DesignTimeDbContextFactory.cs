using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CvarcLogger.Data;

/// <summary>Lets `dotnet ef migrations` construct a DbContext without going through the WPF app's
/// composition root (which has no conventional Program.Main for the EF tooling to discover).</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CvarcLoggerDbContext>
{
    public CvarcLoggerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CvarcLoggerDbContext>();
        string dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CvarcLogger", "cvarclogger.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
        return new CvarcLoggerDbContext(optionsBuilder.Options);
    }
}
