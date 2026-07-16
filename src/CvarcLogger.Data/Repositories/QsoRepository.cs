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
        return qso;
    }

    public async Task UpdateAsync(Qso qso, CancellationToken ct = default)
    {
        qso.ModifiedAtUtc = _clock.UtcNow;
        _db.Qsos.Update(qso);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var qso = await _db.Qsos.FindAsync(new object[] { id }, ct).ConfigureAwait(false);
        if (qso is null) return;
        _db.Qsos.Remove(qso);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
