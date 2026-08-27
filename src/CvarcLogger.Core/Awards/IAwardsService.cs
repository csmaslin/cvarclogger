namespace CvarcLogger.Core.Awards;

public interface IAwardsService
{
    Task<DxccProgress> ComputeDxccProgressAsync(AwardsFilter? filter = null, CancellationToken ct = default);
    Task<WasProgress> ComputeWasProgressAsync(AwardsFilter? filter = null, CancellationToken ct = default);
    Task<IReadOnlyList<BandQsoCount>> ComputeQsoCountsByBandAsync(AwardsFilter? filter = null, CancellationToken ct = default);
}
