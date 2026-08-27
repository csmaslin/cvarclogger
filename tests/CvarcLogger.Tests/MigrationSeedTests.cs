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

    [Fact]
    public async Task SeedIfEmpty_ToppsUpEntitiesMissingFromAnAlreadySeededDatabase()
    {
        // Simulates a database seeded before a later bundled-list expansion: manually inserting one
        // entity (as if it were all a smaller, older seed had produced) must not block the rest of the
        // current list from being added on the next call -- unlike a plain "table already has rows,
        // skip entirely" check would.
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        db.DxccEntities.Add(new CvarcLogger.Core.Models.DxccEntity { EntityCode = 291, EntityName = "United States of America", Continent = "NA" });
        await db.SaveChangesAsync();

        await SeedRunner.SeedIfEmptyAsync(db);

        int count = await db.DxccEntities.CountAsync();
        Assert.True(count > 1, "Seeding should have added entities beyond the one already present.");

        var canada = await db.DxccEntities.FindAsync(1);
        Assert.NotNull(canada);
        Assert.Equal("Canada", canada!.EntityName);
    }

    [Fact]
    public async Task SeedIfEmpty_PopulatesCqAndItuZones()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        await SeedRunner.SeedIfEmptyAsync(db);

        var usa = await db.DxccEntities.FindAsync(291);
        Assert.NotNull(usa);
        Assert.NotNull(usa!.CqZone);
        Assert.NotNull(usa.ItuZone);
    }

    private CvarcLoggerDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CvarcLoggerDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        return new CvarcLoggerDbContext(options);
    }
}
