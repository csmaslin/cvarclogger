using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Models;
using CvarcLogger.Data;
using CvarcLogger.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CvarcLogger.Tests;

/// <summary>Regression coverage for a real crash: the app keeps one DbContext alive for the whole
/// session, so any two callers that each fetch their own AsNoTracking copy of the same QSO and then
/// call UpdateAsync (e.g. the Awards DXCC backfill, then the Edit QSO window's Save) used to throw
/// "the instance of entity type Qso cannot be tracked because another instance with the same key
/// value is already being tracked".</summary>
public class QsoRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CvarcLoggerDbContext _db;
    private readonly QsoRepository _repository;

    public QsoRepositoryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<CvarcLoggerDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new CvarcLoggerDbContext(options);
        _db.Database.EnsureCreated();
        _repository = new QsoRepository(_db, new TestClock());
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task UpdateAsync_TwiceWithSeparatelyFetchedInstances_DoesNotThrow()
    {
        var added = await _repository.AddAsync(new Qso
        {
            Callsign = "W1AW",
            StationCallsign = "N0CALL",
            Band = "20m",
            Mode = "SSB",
            QsoDateTimeOnUtc = DateTime.UtcNow,
        });

        var firstFetch = (await _repository.GetAllAsync()).Single(q => q.Id == added.Id);
        firstFetch.Comment = "first edit";
        await _repository.UpdateAsync(firstFetch);

        var secondFetch = (await _repository.GetAllAsync()).Single(q => q.Id == added.Id);
        secondFetch.Comment = "second edit";
        await _repository.UpdateAsync(secondFetch);

        var final = await _repository.GetByIdAsync(added.Id);
        Assert.Equal("second edit", final!.Comment);
    }

    private class TestClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
