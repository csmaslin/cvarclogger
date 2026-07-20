using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CvarcLogger.Data.Repositories;

public class SotaActivationRepository : ISotaActivationRepository
{
    private readonly CvarcLoggerDbContext _db;

    public SotaActivationRepository(CvarcLoggerDbContext db)
    {
        _db = db;
    }

    public async Task<List<SotaActivation>> GetAllAsync(CancellationToken ct = default) =>
        await _db.SotaActivations.AsNoTracking().OrderBy(s => s.SummitCode).ToListAsync(ct).ConfigureAwait(false);

    public async Task<SotaActivation> AddAsync(SotaActivation activation, CancellationToken ct = default)
    {
        _db.SotaActivations.Add(activation);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        DetachAllTracked();
        return activation;
    }

    public async Task UpdateAsync(SotaActivation activation, CancellationToken ct = default)
    {
        // This repository's DbContext is a single long-lived instance for the whole app session -- an
        // earlier Add/UpdateAsync call for this same Id may still have a *different* instance attached.
        // EF throws if we attach a second instance with the same key, so detach any stale entry first.
        var staleEntry = _db.ChangeTracker.Entries<SotaActivation>()
            .FirstOrDefault(e => e.Entity.Id == activation.Id && !ReferenceEquals(e.Entity, activation));
        if (staleEntry is not null) staleEntry.State = EntityState.Detached;

        _db.SotaActivations.Update(activation);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        DetachAllTracked();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var activation = await _db.SotaActivations.FindAsync(new object[] { id }, ct).ConfigureAwait(false);
        if (activation is null) return;
        _db.SotaActivations.Remove(activation);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Detaches every currently-tracked SotaActivation so it can't conflict with a fresh
    /// (AsNoTracking) instance of the same row on a later call in this same long-lived DbContext -- this
    /// repository never needs to keep anything tracked between calls, it always reloads via
    /// AsNoTracking anyway.</summary>
    private void DetachAllTracked()
    {
        foreach (var entry in _db.ChangeTracker.Entries<SotaActivation>().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
