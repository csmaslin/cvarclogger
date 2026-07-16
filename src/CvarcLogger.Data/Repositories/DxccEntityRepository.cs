using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CvarcLogger.Data.Repositories;

public class DxccEntityRepository : IDxccEntityRepository
{
    private readonly CvarcLoggerDbContext _db;

    public DxccEntityRepository(CvarcLoggerDbContext db)
    {
        _db = db;
    }

    public async Task<List<DxccEntity>> GetAllWithPrefixesAsync(CancellationToken ct = default) =>
        await _db.DxccEntities.AsNoTracking().Include(e => e.Prefixes).ToListAsync(ct).ConfigureAwait(false);

    public async Task<DxccEntity?> GetByCodeAsync(int entityCode, CancellationToken ct = default) =>
        await _db.DxccEntities.AsNoTracking().Include(e => e.Prefixes)
            .FirstOrDefaultAsync(e => e.EntityCode == entityCode, ct).ConfigureAwait(false);
}
