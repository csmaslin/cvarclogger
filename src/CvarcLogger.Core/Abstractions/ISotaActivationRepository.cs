using CvarcLogger.Core.Models;

namespace CvarcLogger.Core.Abstractions;

public interface ISotaActivationRepository
{
    Task<List<SotaActivation>> GetAllAsync(CancellationToken ct = default);
    Task<SotaActivation> AddAsync(SotaActivation activation, CancellationToken ct = default);
    Task UpdateAsync(SotaActivation activation, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
