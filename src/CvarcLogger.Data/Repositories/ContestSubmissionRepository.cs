using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CvarcLogger.Data.Repositories;

public class ContestSubmissionRepository : IContestSubmissionRepository
{
    private readonly CvarcLoggerDbContext _db;

    public ContestSubmissionRepository(CvarcLoggerDbContext db)
    {
        _db = db;
    }

    public async Task<List<ContestSubmission>> GetAllAsync(CancellationToken ct = default) =>
        await _db.ContestSubmissions.AsNoTracking()
            .OrderByDescending(s => s.ModifiedAtUtc)
            .ToListAsync(ct).ConfigureAwait(false);

    public async Task<ContestSubmission?> GetLatestByContestAsync(string contestId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(contestId)) return null;
        return await _db.ContestSubmissions.AsNoTracking()
            .Where(s => s.ContestId == contestId)
            .OrderByDescending(s => s.ModifiedAtUtc)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    public async Task<ContestSubmission> AddAsync(ContestSubmission submission, CancellationToken ct = default)
    {
        submission.CreatedAtUtc = DateTime.UtcNow;
        submission.ModifiedAtUtc = submission.CreatedAtUtc;
        _db.ContestSubmissions.Add(submission);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return submission;
    }

    public async Task UpdateAsync(ContestSubmission submission, CancellationToken ct = default)
    {
        submission.ModifiedAtUtc = DateTime.UtcNow;
        _db.ContestSubmissions.Update(submission);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var existing = await _db.ContestSubmissions.FindAsync(new object?[] { id }, ct).ConfigureAwait(false);
        if (existing is null) return;
        _db.ContestSubmissions.Remove(existing);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
