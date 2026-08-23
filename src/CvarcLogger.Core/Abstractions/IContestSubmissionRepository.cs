using CvarcLogger.Core.Models;

namespace CvarcLogger.Core.Abstractions;

public interface IContestSubmissionRepository
{
    Task<List<ContestSubmission>> GetAllAsync(CancellationToken ct = default);

    /// <summary>The most-recently-modified submission for this contest id, or null if none exists.
    /// Used at export time to pre-fill the export dialog with the last header this operator used for
    /// the same contest -- typing "ARRL-DX-CW" a second time shouldn't require re-entering the whole
    /// address block.</summary>
    Task<ContestSubmission?> GetLatestByContestAsync(string contestId, CancellationToken ct = default);

    Task<ContestSubmission> AddAsync(ContestSubmission submission, CancellationToken ct = default);
    Task UpdateAsync(ContestSubmission submission, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
