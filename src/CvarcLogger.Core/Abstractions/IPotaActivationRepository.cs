using CvarcLogger.Core.Models;

namespace CvarcLogger.Core.Abstractions;

public interface IPotaActivationRepository
{
    Task<List<PotaActivation>> GetAllAsync(CancellationToken ct = default);
    Task<PotaActivation> AddAsync(PotaActivation activation, CancellationToken ct = default);
    Task UpdateAsync(PotaActivation activation, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
