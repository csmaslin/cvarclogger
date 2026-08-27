using CvarcLogger.Core.Models;

namespace CvarcLogger.Core.Scoring;

/// <summary>Score breakdown for one CQ WW event (SSB or CW), already filtered to that event's date
/// window, eligible modes, and the six scoring bands.</summary>
public record CqWwScoreBreakdown(
    int QsoCount,
    int QsoPoints,
    int ZoneMultiplier,
    int CountryMultiplier,
    int Score,
    int UnresolvedCount);

/// <summary>CQ WW DX Contest scoring: QSO points depend on the worked station's relationship to the
/// operator's own station (same country = 0 but still credits multipliers; same continent = 1, except
/// both stations in North America = 2; different continent = 3). Zone and country multipliers are each
/// counted once per band, then summed across bands, and the final score is QSO points times the sum of
/// both multiplier totals -- see https://www.cqww.com/rules.htm.</summary>
public static class CqWwScorer
{
    public static readonly IReadOnlySet<string> ScoringBands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "160m", "80m", "40m", "20m", "15m", "10m",
    };

    /// <summary>Scores an already date/mode-filtered set of QSOs. <paramref name="myEntityByStationCallsign"/>
    /// maps each QSO's StationCallsign (the operator's own callsign at log time) to the resolved DXCC
    /// entity for that callsign -- callers must resolve this themselves (see ICallsignEntityResolver)
    /// since resolution is async and this scorer is a pure, synchronous function like FieldDayScorer.</summary>
    public static CqWwScoreBreakdown Score(
        IEnumerable<Qso> qsos,
        IReadOnlyDictionary<string, DxccEntity?> myEntityByStationCallsign)
    {
        int qsoCount = 0;
        int qsoPoints = 0;
        int unresolvedCount = 0;

        var zonesByBand = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        var countriesByBand = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

        foreach (var qso in qsos)
        {
            if (string.IsNullOrWhiteSpace(qso.Band) || !ScoringBands.Contains(qso.Band))
                continue;

            bool myEntityKnown = myEntityByStationCallsign.TryGetValue(qso.StationCallsign, out var myEntity) && myEntity is not null;

            if (!myEntityKnown || qso.DxccEntityCode is null || qso.CqZone is null || string.IsNullOrWhiteSpace(qso.Continent))
            {
                unresolvedCount++;
                continue;
            }

            qsoCount++;
            qsoPoints += PointsFor(qso, myEntity!);

            if (!zonesByBand.TryGetValue(qso.Band, out var zones))
                zonesByBand[qso.Band] = zones = new HashSet<int>();
            zones.Add(qso.CqZone.Value);

            if (!countriesByBand.TryGetValue(qso.Band, out var countries))
                countriesByBand[qso.Band] = countries = new HashSet<int>();
            countries.Add(qso.DxccEntityCode.Value);
        }

        int zoneMultiplier = zonesByBand.Values.Sum(set => set.Count);
        int countryMultiplier = countriesByBand.Values.Sum(set => set.Count);
        int score = qsoPoints * (zoneMultiplier + countryMultiplier);

        return new CqWwScoreBreakdown(qsoCount, qsoPoints, zoneMultiplier, countryMultiplier, score, unresolvedCount);
    }

    private static int PointsFor(Qso qso, DxccEntity myEntity)
    {
        if (qso.DxccEntityCode == myEntity.EntityCode)
            return 0; // same country: no QSO points, but still credits zone/country multipliers above.

        bool sameContinent = string.Equals(qso.Continent, myEntity.Continent, StringComparison.OrdinalIgnoreCase);
        if (!sameContinent)
            return 3;

        bool bothNorthAmerica = string.Equals(myEntity.Continent, "NA", StringComparison.OrdinalIgnoreCase);
        return bothNorthAmerica ? 2 : 1;
    }
}
