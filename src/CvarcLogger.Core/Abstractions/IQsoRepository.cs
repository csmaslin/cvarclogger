using CvarcLogger.Core.Models;

namespace CvarcLogger.Core.Abstractions;

public interface IQsoRepository
{
    Task<List<Qso>> GetAllAsync(CancellationToken ct = default);
    Task<Qso?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Qso> AddAsync(Qso qso, CancellationToken ct = default);
    Task UpdateAsync(Qso qso, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
