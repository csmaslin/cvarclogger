using CvarcLogger.Core.Models;

namespace CvarcLogger.Core.Scoring;

public record SprintScoreBreakdown(int QsoCount, int Multiplier, int Score);

/// <summary>Scoring shared by the NCJ-style CW/SSB/RTTY Sprints and NAQP: Score = total valid QSOs x (US
/// states + DC + the 13 Canadian provinces/territories + other North American DXCC countries worked).
/// Non-North-American contacts count toward the QSO total but grant no multiplier. USA and Canada
/// themselves never count as "countries" -- only their individual states/provinces do, via the worked
/// station's State field (which this app already stores using the same two-letter codes for both, per
/// ArrlSectionResolver's precedent). Hawaii (KH6) is just one more entry in that same US-states set, so
/// it needs no special-casing beyond being present in the list.
///
/// The two contests differ on how the multiplier is accumulated: the Sprints count each multiplier once
/// for the whole event regardless of band, while NAQP's rule 11 explicitly says multipliers "count again
/// on each band" -- pass multiplierPerBand accordingly.</summary>
public static class SprintScorer
{
    private const int UsaEntityCode = 291;
    private const int CanadaEntityCode = 1;

    public static readonly IReadOnlySet<string> UsStatesAndCanadianProvinces = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "AL", "AK", "AZ", "AR", "CA", "CO", "CT", "DE", "FL", "GA", "HI", "ID", "IL", "IN", "IA", "KS",
        "KY", "LA", "ME", "MD", "MA", "MI", "MN", "MS", "MO", "MT", "NE", "NV", "NH", "NJ", "NM", "NY",
        "NC", "ND", "OH", "OK", "OR", "PA", "RI", "SC", "SD", "TN", "TX", "UT", "VT", "VA", "WA", "WV",
        "WI", "WY", "DC",
        "BC", "AB", "SK", "MB", "ON", "QC", "NB", "NS", "PE", "NL", "YT", "NT", "NU",
    };

    public static SprintScoreBreakdown Score(IEnumerable<Qso> qsos, IReadOnlySet<string> validBands, bool multiplierPerBand = false)
    {
        int qsoCount = 0;
        var multiplierKeysTotal = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var multiplierKeysByBand = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var qso in qsos)
        {
            if (string.IsNullOrWhiteSpace(qso.Band) || !validBands.Contains(qso.Band))
                continue;

            qsoCount++;

            string? key = MultiplierKeyFor(qso);
            if (key is null)
                continue;

            if (multiplierPerBand)
            {
                if (!multiplierKeysByBand.TryGetValue(qso.Band, out var bandSet))
                    multiplierKeysByBand[qso.Band] = bandSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                bandSet.Add(key);
            }
            else
            {
                multiplierKeysTotal.Add(key);
            }
        }

        int multiplier = multiplierPerBand
            ? multiplierKeysByBand.Values.Sum(set => set.Count)
            : multiplierKeysTotal.Count;

        return new SprintScoreBreakdown(qsoCount, multiplier, qsoCount * multiplier);
    }

    private static string? MultiplierKeyFor(Qso qso)
    {
        string? state = qso.State?.Trim();
        if (!string.IsNullOrEmpty(state) && UsStatesAndCanadianProvinces.Contains(state))
            return "SP:" + state.ToUpperInvariant();

        if (qso.DxccEntityCode is null || !string.Equals(qso.Continent, "NA", StringComparison.OrdinalIgnoreCase))
            return null;

        if (qso.DxccEntityCode == UsaEntityCode || qso.DxccEntityCode == CanadaEntityCode)
            return null; // US/Canada contact with no resolved state/province: no state to credit, and must not fall back to counting as a "country" either.

        return "CTRY:" + qso.DxccEntityCode.Value;
    }
}
