using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CvarcLogger.Data.Repositories;

public class StationProfileRepository : IStationProfileRepository
{
    private readonly CvarcLoggerDbContext _db;

    public StationProfileRepository(CvarcLoggerDbContext db)
    {
        _db = db;
    }

    public async Task<List<StationProfile>> GetAllAsync(CancellationToken ct = default) =>
        await _db.StationProfiles.AsNoTracking().OrderBy(s => s.Callsign).ToListAsync(ct).ConfigureAwait(false);

    public async Task<StationProfile?> GetDefaultAsync(CancellationToken ct = default) =>
        await _db.StationProfiles.AsNoTracking().FirstOrDefaultAsync(s => s.IsDefault, ct).ConfigureAwait(false)
        ?? await _db.StationProfiles.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync(ct).ConfigureAwait(false);

    public async Task<StationProfile> AddAsync(StationProfile profile, CancellationToken ct = default)
    {
        if (profile.IsDefault)
        {
            await ClearOtherDefaultsAsync(ct).ConfigureAwait(false);
        }
        _db.StationProfiles.Add(profile);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        DetachAllTracked();
        return profile;
    }

    public async Task UpdateAsync(StationProfile profile, CancellationToken ct = default)
    {
        if (profile.IsDefault)
        {
            await ClearOtherDefaultsAsync(ct, profile.Id).ConfigureAwait(false);
        }

        // The profile passed in is almost always a fresh instance from an AsNoTracking() query, but
        // this repository's DbContext is a single long-lived instance for the whole app session -- an
        // earlier Add/UpdateAsync call for this same Id may still have a *different* instance attached.
        // EF throws if we attach a second instance with the same key, so detach any stale entry first.
        var staleEntry = _db.ChangeTracker.Entries<StationProfile>()
            .FirstOrDefault(e => e.Entity.Id == profile.Id && !ReferenceEquals(e.Entity, profile));
        if (staleEntry is not null) staleEntry.State = EntityState.Detached;

        _db.StationProfiles.Update(profile);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        DetachAllTracked();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var profile = await _db.StationProfiles.FindAsync(new object[] { id }, ct).ConfigureAwait(false);
        if (profile is null) return;
        _db.StationProfiles.Remove(profile);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task ClearOtherDefaultsAsync(CancellationToken ct, int? exceptId = null)
    {
        var others = await _db.StationProfiles
            .Where(s => s.IsDefault && (exceptId == null || s.Id != exceptId))
            .ToListAsync(ct).ConfigureAwait(false);
        foreach (var other in others)
        {
            other.IsDefault = false;
        }
    }

    /// <summary>Detaches every currently-tracked StationProfile so it can't conflict with a fresh
    /// (AsNoTracking) instance of the same row on a later call in this same long-lived DbContext -- this
    /// repository never needs to keep anything tracked between calls, it always reloads via
    /// AsNoTracking anyway.</summary>
    private void DetachAllTracked()
    {
        foreach (var entry in _db.ChangeTracker.Entries<StationProfile>().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
