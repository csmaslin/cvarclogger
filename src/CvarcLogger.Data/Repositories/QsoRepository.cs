using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CvarcLogger.Data.Repositories;

public class QsoRepository : IQsoRepository
{
    private readonly CvarcLoggerDbContext _db;
    private readonly IClock _clock;

    public QsoRepository(CvarcLoggerDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<List<Qso>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Qsos.AsNoTracking().OrderByDescending(q => q.QsoDateTimeOnUtc).ToListAsync(ct).ConfigureAwait(false);

    public async Task<Qso?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _db.Qsos.FindAsync(new object[] { id }, ct).ConfigureAwait(false);

    public async Task<Qso> AddAsync(Qso qso, CancellationToken ct = default)
    {
        qso.CreatedAtUtc = _clock.UtcNow;
        qso.ModifiedAtUtc = _clock.UtcNow;
        _db.Qsos.Add(qso);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        // Detach so this instance doesn't linger tracked for the rest of the app session -- a later
        // UpdateAsync on a fresh (AsNoTracking) copy of this same QSO would otherwise conflict with it.
        _db.Entry(qso).State = EntityState.Detached;
        return qso;
    }

    public async Task UpdateAsync(Qso qso, CancellationToken ct = default)
    {
        qso.ModifiedAtUtc = _clock.UtcNow;

        // The QSO passed in is almost always a fresh instance from an AsNoTracking() query, but this
        // repository's DbContext is a single long-lived instance for the whole app session -- an
        // earlier UpdateAsync call (e.g. the awards DXCC backfill) may still have a *different* instance
        // for this same Id attached. EF throws if we attach a second instance with the same key, so
        // detach any stale entry first.
        var staleEntry = _db.ChangeTracker.Entries<Qso>()
            .FirstOrDefault(e => e.Entity.Id == qso.Id && !ReferenceEquals(e.Entity, qso));
        if (staleEntry is not null) staleEntry.State = EntityState.Detached;

        _db.Qsos.Update(qso);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        // Detach afterward too, so this instance doesn't linger and cause the same conflict for the
        // next caller that fetches a fresh copy of this QSO.
        _db.Entry(qso).State = EntityState.Detached;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var qso = await _db.Qsos.FindAsync(new object[] { id }, ct).ConfigureAwait(false);
        if (qso is null) return;
        _db.Qsos.Remove(qso);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> DeleteAllAsync(CancellationToken ct = default)
    {
        // Bulk delete straight against the database (no per-row load). Any entities this long-lived
        // context happened to be tracking are cleared afterward so a later read doesn't resurrect a
        // now-deleted row from the change tracker.
        int removed = await _db.Qsos.ExecuteDeleteAsync(ct).ConfigureAwait(false);
        _db.ChangeTracker.Clear();
        return removed;
    }
}
