using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Models;

namespace CvarcLogger.Core.Awards;

public class AwardsService : IAwardsService
{
    // The three DXCC entities that together make up the WAS award footprint: USA (mainland), Alaska, Hawaii.
    private static readonly int[] WasEligibleEntityCodes = { 291, 6, 110 };

    private static readonly string[] UsStates =
    {
        "AL", "AK", "AZ", "AR", "CA", "CO", "CT", "DE", "FL", "GA", "HI", "ID", "IL", "IN", "IA",
        "KS", "KY", "LA", "ME", "MD", "MA", "MI", "MN", "MS", "MO", "MT", "NE", "NV", "NH", "NJ",
        "NM", "NY", "NC", "ND", "OH", "OK", "OR", "PA", "RI", "SC", "SD", "TN", "TX", "UT", "VT",
        "VA", "WA", "WV", "WI", "WY"
    };

    private readonly IQsoRepository _qsoRepository;
    private readonly IDxccEntityRepository _dxccRepository;

    public AwardsService(IQsoRepository qsoRepository, IDxccEntityRepository dxccRepository)
    {
        _qsoRepository = qsoRepository;
        _dxccRepository = dxccRepository;
    }

    public async Task<DxccProgress> ComputeDxccProgressAsync(AwardsFilter? filter = null, CancellationToken ct = default)
    {
        var qsos = await GetFilteredQsosAsync(filter, ct).ConfigureAwait(false);
        var entities = await _dxccRepository.GetAllWithPrefixesAsync(ct).ConfigureAwait(false);
        var entityByCode = entities.ToDictionary(e => e.EntityCode);

        var statuses = qsos
            .Where(q => q.DxccEntityCode.HasValue)
            .GroupBy(q => q.DxccEntityCode!.Value)
            .Select(group =>
            {
                string entityName = entityByCode.TryGetValue(group.Key, out var e) ? e.EntityName : $"Entity {group.Key}";
                return new DxccEntityStatus(group.Key, entityName, Worked: true, Confirmed: group.Any(IsConfirmed));
            })
            .OrderBy(s => s.EntityName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DxccProgress(
            WorkedCount: statuses.Count,
            ConfirmedCount: statuses.Count(s => s.Confirmed),
            Entities: statuses);
    }

    public async Task<WasProgress> ComputeWasProgressAsync(AwardsFilter? filter = null, CancellationToken ct = default)
    {
        var qsos = await GetFilteredQsosAsync(filter, ct).ConfigureAwait(false);

        var byState = qsos
            .Where(q => q.DxccEntityCode.HasValue
                        && WasEligibleEntityCodes.Contains(q.DxccEntityCode.Value)
                        && !string.IsNullOrWhiteSpace(q.State))
            .GroupBy(q => q.State!.Trim().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.Any(IsConfirmed));

        var statuses = UsStates
            .Select(state => new WasStateStatus(
                state,
                Worked: byState.ContainsKey(state),
                Confirmed: byState.TryGetValue(state, out var confirmed) && confirmed))
            .ToList();

        return new WasProgress(
            WorkedCount: statuses.Count(s => s.Worked),
            ConfirmedCount: statuses.Count(s => s.Confirmed),
            States: statuses);
    }

    private async Task<List<Qso>> GetFilteredQsosAsync(AwardsFilter? filter, CancellationToken ct)
    {
        var qsos = await _qsoRepository.GetAllAsync(ct).ConfigureAwait(false);
        if (filter is null) return qsos;

        IEnumerable<Qso> result = qsos;
        if (!string.IsNullOrWhiteSpace(filter.Band))
            result = result.Where(q => string.Equals(q.Band, filter.Band, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.Mode))
            result = result.Where(q => string.Equals(q.Mode, filter.Mode, StringComparison.OrdinalIgnoreCase));
        return result.ToList();
    }

    private static bool IsConfirmed(Qso q) =>
        q.QslRcvd is QslStatus.Sent or QslStatus.Verified ||
        q.LotwQslRcvd is QslStatus.Sent or QslStatus.Verified;
}
