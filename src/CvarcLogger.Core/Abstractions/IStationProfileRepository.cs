using CvarcLogger.Core.Models;

namespace CvarcLogger.Core.Abstractions;

public interface IStationProfileRepository
{
    Task<List<StationProfile>> GetAllAsync(CancellationToken ct = default);
    Task<StationProfile?> GetDefaultAsync(CancellationToken ct = default);
    Task<StationProfile> AddAsync(StationProfile profile, CancellationToken ct = default);
    Task UpdateAsync(StationProfile profile, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
