using CvarcLogger.Core.Models;

namespace CvarcLogger.Core.Scoring;

public record ArrlDxScoreBreakdown(int QsoCount, int QsoPoints, int Multiplier, int Score, int UnresolvedCount);

/// <summary>Scoring for the ARRL International DX Contest (CW and Phone events scored identically):
/// W/VE stations may only contact DX stations and vice versa (contacts on the same "side" don't count,
/// per rule 2.3). Every valid contact is a flat 3 QSO points. Multiplier is counted once per band:
/// a W/VE station's multiplier is DXCC entities worked (Hawaii/Alaska count as DX entities here, unlike
/// most other contests); a DX station's multiplier is US states/DC/Canadian provinces worked. See
/// https://contests.arrl.org/ContestRules/DX-Rules.pdf.</summary>
public static class ArrlDxScorer
{
    private const int UsaEntityCode = 291;
    private const int CanadaEntityCode = 1;
    private const int QsoPointsPerContact = 3;

    public static readonly IReadOnlySet<string> ScoringBands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "160m", "80m", "40m", "20m", "15m", "10m",
    };

    public static ArrlDxScoreBreakdown Score(
        IEnumerable<Qso> qsos,
        IReadOnlyDictionary<string, DxccEntity?> myEntityByStationCallsign)
    {
        int qsoCount = 0;
        int unresolvedCount = 0;
        var multByBand = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var qso in qsos)
        {
            if (string.IsNullOrWhiteSpace(qso.Band) || !ScoringBands.Contains(qso.Band))
                continue;

            bool myEntityKnown = myEntityByStationCallsign.TryGetValue(qso.StationCallsign, out var myEntity) && myEntity is not null;
            if (!myEntityKnown || qso.DxccEntityCode is null)
            {
                unresolvedCount++;
                continue;
            }

            bool amWve = IsWve(myEntity!.EntityCode);
            bool workedIsWve = IsWve(qso.DxccEntityCode.Value);

            string? multKey;
            if (amWve)
            {
                if (workedIsWve) { unresolvedCount++; continue; } // W/VE-to-W/VE doesn't count, rule 2.3
                multKey = "ENT:" + qso.DxccEntityCode.Value;
            }
            else
            {
                if (!workedIsWve) { unresolvedCount++; continue; } // DX-to-DX doesn't count, rule 2.3
                string? state = qso.State?.Trim();
                if (string.IsNullOrEmpty(state)) { unresolvedCount++; continue; }
                multKey = "SP:" + state.ToUpperInvariant();
            }

            qsoCount++;
            if (!multByBand.TryGetValue(qso.Band, out var bandSet))
                multByBand[qso.Band] = bandSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bandSet.Add(multKey);
        }

        int qsoPoints = qsoCount * QsoPointsPerContact;
        int multiplier = multByBand.Values.Sum(set => set.Count);
        return new ArrlDxScoreBreakdown(qsoCount, qsoPoints, multiplier, qsoPoints * multiplier, unresolvedCount);
    }

    private static bool IsWve(int entityCode) => entityCode == UsaEntityCode || entityCode == CanadaEntityCode;
}
