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
        return profile;
    }

    public async Task UpdateAsync(StationProfile profile, CancellationToken ct = default)
    {
        if (profile.IsDefault)
        {
            await ClearOtherDefaultsAsync(ct, profile.Id).ConfigureAwait(false);
        }
        _db.StationProfiles.Update(profile);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
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
}
