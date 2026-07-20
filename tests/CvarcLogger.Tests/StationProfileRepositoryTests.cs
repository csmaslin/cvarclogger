using CvarcLogger.Core.Models;
using CvarcLogger.Data;
using CvarcLogger.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CvarcLogger.Tests;

/// <summary>Regression coverage for a real crash: editing a station profile (e.g. clearing Operator
/// Callsign) and saving worked once per app session, then crashed on the next save -- the app keeps one
/// DbContext alive for the whole session, so a second AsNoTracking-fetched instance of the same profile
/// hit "the instance of entity type StationProfile cannot be tracked because another instance with the
/// same key value is already being tracked". See QsoRepositoryTests for the same pattern, already fixed
/// there.</summary>
public class StationProfileRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CvarcLoggerDbContext _db;
    private readonly StationProfileRepository _repository;

    public StationProfileRepositoryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<CvarcLoggerDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new CvarcLoggerDbContext(options);
        _db.Database.EnsureCreated();
        _repository = new StationProfileRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task UpdateAsync_TwiceWithSeparatelyFetchedInstances_DoesNotThrow()
    {
        var added = await _repository.AddAsync(new StationProfile
        {
            Callsign = "W1AW",
            OperatorCallsign = "N0CALL",
        });

        var firstFetch = (await _repository.GetAllAsync()).Single(p => p.Id == added.Id);
        firstFetch.OperatorCallsign = null;
        await _repository.UpdateAsync(firstFetch);

        var secondFetch = (await _repository.GetAllAsync()).Single(p => p.Id == added.Id);
        await _repository.UpdateAsync(secondFetch);

        var final = (await _repository.GetAllAsync()).Single(p => p.Id == added.Id);
        Assert.Null(final.OperatorCallsign);
    }

    [Fact]
    public async Task UpdateAsync_SettingIsDefault_ClearsOtherDefaultsAcrossSeparateCalls()
    {
        var first = await _repository.AddAsync(new StationProfile { Callsign = "W1AW", IsDefault = true });
        var second = await _repository.AddAsync(new StationProfile { Callsign = "K1ABC" });

        var secondFetch = (await _repository.GetAllAsync()).Single(p => p.Id == second.Id);
        secondFetch.IsDefault = true;
        await _repository.UpdateAsync(secondFetch);

        // Re-saving the first profile (now no longer default) in the same session used to conflict with
        // the tracked entity ClearOtherDefaultsAsync picked up while clearing it during the call above.
        var firstFetch = (await _repository.GetAllAsync()).Single(p => p.Id == first.Id);
        await _repository.UpdateAsync(firstFetch);

        var all = await _repository.GetAllAsync();
        Assert.False(all.Single(p => p.Id == first.Id).IsDefault);
        Assert.True(all.Single(p => p.Id == second.Id).IsDefault);
    }
}
