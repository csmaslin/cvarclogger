using CvarcLogger.Core.Models;

namespace CvarcLogger.Core.Abstractions;

public interface IDxccEntityRepository
{
    /// <summary>All DXCC entities with their prefix mappings loaded, for in-memory resolution/awards computation.</summary>
    Task<List<DxccEntity>> GetAllWithPrefixesAsync(CancellationToken ct = default);
    Task<DxccEntity?> GetByCodeAsync(int entityCode, CancellationToken ct = default);
}
