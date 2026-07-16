using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Awards;
using CvarcLogger.Core.Models;
using CvarcLogger.Data;
using CvarcLogger.Data.Repositories;
using CvarcLogger.Data.Seed;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CvarcLogger.Tests;

/// <summary>Uses a real (in-memory-mode) SQLite connection via Microsoft.Data.Sqlite rather than the
/// EF Core InMemory provider, so these tests exercise real SQL translation, not EF's in-memory shim.</summary>
public class AwardsServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CvarcLoggerDbContext _db;

    public AwardsServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<CvarcLoggerDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new CvarcLoggerDbContext(options);
        _db.Database.EnsureCreated();
        SeedRunner.SeedIfEmptyAsync(_db).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ComputeDxccProgress_CountsWorkedAndConfirmedEntities()
    {
        AddQso("W1AW", dxccCode: 291, qslRcvd: QslStatus.NotSent);
        AddQso("VE3ABC", dxccCode: 1, qslRcvd: QslStatus.Sent);
        AddQso("JA1ABC", dxccCode: 339, qslRcvd: QslStatus.NotSent);
        await _db.SaveChangesAsync();

        var progress = await CreateService().ComputeDxccProgressAsync();

        Assert.Equal(3, progress.WorkedCount);
        Assert.Equal(1, progress.ConfirmedCount);
    }

    [Fact]
    public async Task ComputeWasProgress_TreatsMainlandAlaskaHawaiiAsOneCombinedAward()
    {
        AddQso("W1AW", dxccCode: 291, state: "CT", qslRcvd: QslStatus.Sent);
        AddQso("KL7AB", dxccCode: 6, state: "AK", qslRcvd: QslStatus.NotSent);
        AddQso("KH6XX", dxccCode: 110, state: "HI", qslRcvd: QslStatus.Sent);
        await _db.SaveChangesAsync();

        var progress = await CreateService().ComputeWasProgressAsync();

        Assert.Equal(3, progress.WorkedCount);
        Assert.Equal(2, progress.ConfirmedCount);
        Assert.Contains(progress.States, s => s.State == "AK" && s.Worked && !s.Confirmed);
        Assert.Contains(progress.States, s => s.State == "HI" && s.Worked && s.Confirmed);
    }

    [Fact]
    public async Task ComputeDxccProgress_FiltersByBandWhenRequested()
    {
        AddQso("W1AW", dxccCode: 291, band: "20m");
        AddQso("VE3ABC", dxccCode: 1, band: "40m");
        await _db.SaveChangesAsync();

        var progress = await CreateService().ComputeDxccProgressAsync(new AwardsFilter(Band: "20m"));

        Assert.Equal(1, progress.WorkedCount);
        Assert.Equal("United States of America", progress.Entities.Single().EntityName);
    }

    private void AddQso(string callsign, int dxccCode, string? state = null, string band = "20m", QslStatus qslRcvd = QslStatus.NotSent)
    {
        _db.Qsos.Add(new Qso
        {
            Callsign = callsign,
            StationCallsign = "N0CALL",
            Band = band,
            Mode = "SSB",
            QsoDateTimeOnUtc = DateTime.UtcNow,
            DxccEntityCode = dxccCode,
            State = state,
            QslRcvd = qslRcvd,
        });
    }

    private AwardsService CreateService() =>
        new(new QsoRepository(_db, new TestClock()), new DxccEntityRepository(_db));

    private class TestClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
