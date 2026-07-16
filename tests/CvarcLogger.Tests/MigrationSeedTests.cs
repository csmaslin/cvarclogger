using CvarcLogger.Data;
using CvarcLogger.Data.Seed;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CvarcLogger.Tests;

public class MigrationSeedTests : IDisposable
{
    private readonly string _dbPath;

    public MigrationSeedTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"cvarclogger-test-{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        // Microsoft.Data.Sqlite pools native connections by default, which can keep a file handle
        // open even after the DbContext that used it is disposed.
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task Migrate_CreatesSchemaOnFreshDatabase()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        Assert.True(File.Exists(_dbPath));
        Assert.Empty(await db.Qsos.ToListAsync());
    }

    [Fact]
    public async Task SeedIfEmpty_PopulatesDxccEntitiesOnFreshDatabase()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        await SeedRunner.SeedIfEmptyAsync(db);

        int count = await db.DxccEntities.CountAsync();
        Assert.True(count > 0);

        var usa = await db.DxccEntities.FindAsync(291);
        Assert.NotNull(usa);
        Assert.Equal("United States of America", usa!.EntityName);
    }

    [Fact]
    public async Task SeedIfEmpty_IsIdempotent()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        await SeedRunner.SeedIfEmptyAsync(db);
        int firstCount = await db.DxccEntities.CountAsync();

        await SeedRunner.SeedIfEmptyAsync(db);
        int secondCount = await db.DxccEntities.CountAsync();

        Assert.Equal(firstCount, secondCount);
    }

    private CvarcLoggerDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CvarcLoggerDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        return new CvarcLoggerDbContext(options);
    }
}
