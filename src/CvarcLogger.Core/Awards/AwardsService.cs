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
    private readonly ICallsignEntityResolver _entityResolver;

    public AwardsService(IQsoRepository qsoRepository, IDxccEntityRepository dxccRepository, ICallsignEntityResolver entityResolver)
    {
        _qsoRepository = qsoRepository;
        _dxccRepository = dxccRepository;
        _entityResolver = entityResolver;
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

    private static readonly string[] PhoneModes = { "SSB", "FM", "AM" };
    private static readonly string[] CwModes = { "CW" };
    private static readonly string[] DigitalModes = { "FT8", "FT4", "RTTY", "PSK", "DIGITALVOICE" };

    public async Task<WasProgress> ComputeWasProgressAsync(AwardsFilter? filter = null, CancellationToken ct = default)
    {
        var qsos = await GetFilteredQsosAsync(filter, ct).ConfigureAwait(false);

        var eligible = qsos
            .Where(q => q.DxccEntityCode.HasValue
                        && WasEligibleEntityCodes.Contains(q.DxccEntityCode.Value)
                        && !string.IsNullOrWhiteSpace(q.State))
            .ToList();

        var byState = eligible
            .GroupBy(q => q.State!.Trim().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.Any(IsConfirmed));

        var phoneStates = StatesWorkedIn(eligible, PhoneModes);
        var cwStates = StatesWorkedIn(eligible, CwModes);
        var digitalStates = StatesWorkedIn(eligible, DigitalModes);

        var statuses = UsStates
            .Select(state => new WasStateStatus(
                state,
                Worked: byState.ContainsKey(state),
                Confirmed: byState.TryGetValue(state, out var confirmed) && confirmed,
                Phone: phoneStates.Contains(state),
                Cw: cwStates.Contains(state),
                Digital: digitalStates.Contains(state)))
            .ToList();

        return new WasProgress(
            WorkedCount: statuses.Count(s => s.Worked),
            ConfirmedCount: statuses.Count(s => s.Confirmed),
            States: statuses);
    }

    private static HashSet<string> StatesWorkedIn(IEnumerable<Qso> eligibleQsos, string[] modes) =>
        eligibleQsos
            .Where(q => modes.Contains(q.Mode, StringComparer.OrdinalIgnoreCase))
            .Select(q => q.State!.Trim().ToUpperInvariant())
            .ToHashSet();

    /// <summary>Plain per-band QSO volume -- every logged QSO on that band, regardless of DXCC entity
    /// resolution or confirmation, unlike ComputeDxccProgressAsync's entity-based counts. Ordered by
    /// QsoFieldOptions.Bands' canonical band order (ADIF band list, low to high frequency); any Band value
    /// not in that list (a typo, or a band ADIF doesn't define) sorts alphabetically after the known ones
    /// rather than being dropped. Bands with zero QSOs are omitted.</summary>
    public async Task<IReadOnlyList<BandQsoCount>> ComputeQsoCountsByBandAsync(AwardsFilter? filter = null, CancellationToken ct = default)
    {
        var qsos = await GetFilteredQsosAsync(filter, ct).ConfigureAwait(false);

        var counts = qsos
            .Where(q => !string.IsNullOrWhiteSpace(q.Band))
            .GroupBy(q => q.Band!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var known = QsoFieldOptions.Bands
            .Where(counts.ContainsKey)
            .Select(band => new BandQsoCount(band, counts[band]));

        var unknown = counts.Keys
            .Where(band => !QsoFieldOptions.Bands.Contains(band, StringComparer.OrdinalIgnoreCase))
            .OrderBy(band => band, StringComparer.OrdinalIgnoreCase)
            .Select(band => new BandQsoCount(band, counts[band]));

        return known.Concat(unknown).ToList();
    }

    private async Task<List<Qso>> GetFilteredQsosAsync(AwardsFilter? filter, CancellationToken ct)
    {
        var qsos = await _qsoRepository.GetAllAsync(ct).ConfigureAwait(false);
        await BackfillMissingDxccEntitiesAsync(qsos, ct).ConfigureAwait(false);

        if (filter is null) return qsos;

        IEnumerable<Qso> result = qsos;
        if (!string.IsNullOrWhiteSpace(filter.Band))
            result = result.Where(q => string.Equals(q.Band, filter.Band, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.Mode))
            result = result.Where(q => string.Equals(q.Mode, filter.Mode, StringComparison.OrdinalIgnoreCase));
        return result.ToList();
    }

    /// <summary>QSOs logged before entity resolution existed, or imported from ADIF without a DXCC tag,
    /// are left with a null DxccEntityCode and silently drop out of both the DXCC and WAS award tallies.
    /// Resolve and persist those on the fly whenever awards are computed, skipping any QSO the user has
    /// manually corrected (DxccEntityOverride) so this never clobbers a deliberate fix.</summary>
    private async Task BackfillMissingDxccEntitiesAsync(List<Qso> qsos, CancellationToken ct)
    {
        foreach (var qso in qsos)
        {
            if (qso.DxccEntityCode.HasValue || qso.DxccEntityOverride) continue;

            var resolved = await _entityResolver.ResolveAsync(qso.Callsign, ct).ConfigureAwait(false);
            if (resolved is null) continue;

            qso.DxccEntityCode = resolved.EntityCode;
            qso.Continent ??= resolved.Continent;
            qso.CqZone ??= resolved.CqZone;
            qso.ItuZone ??= resolved.ItuZone;
            await _qsoRepository.UpdateAsync(qso, ct).ConfigureAwait(false);
        }
    }

    private static bool IsConfirmed(Qso q) =>
        q.QslRcvd is QslStatus.Sent or QslStatus.Verified ||
        q.LotwQslRcvd is QslStatus.Sent or QslStatus.Verified;
}
