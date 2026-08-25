using CvarcLogger.Core.Models;

namespace CvarcLogger.Core.Scoring;

public record OneSixtyMeterScoreBreakdown(int QsoCount, int QsoPoints, int Multiplier, int Score, int UnresolvedCount);

/// <summary>Scoring for the ARRL 160-Meter Contest: single band (1.8 MHz), CW only. W/VE stations may
/// contact any other station (W/VE or DX); DX stations may only contact W/VE stations (DX-to-DX doesn't
/// count, rule 2.2). A contact where both stations are W/VE is worth 2 QSO points; a contact crossing
/// the W/VE/DX boundary is worth 5. The multiplier for a W/VE station is ARRL/RAC sections plus DXCC
/// entities worked (one combined pool, rule 5.2.1); for a DX station it's ARRL/RAC sections only (rule
/// 5.2.2). Single band, so the multiplier is counted once overall, not per band. See
/// https://contests.arrl.org/ContestRules/160M-Rules.pdf.</summary>
public static class OneSixtyMeterScorer
{
    private const int UsaEntityCode = 291;
    private const int CanadaEntityCode = 1;
    private const int WveToWvePoints = 2;
    private const int CrossBoundaryPoints = 5;

    public static readonly IReadOnlySet<string> ScoringBands = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "160m" };
    private static readonly HashSet<string> CwModes = new(StringComparer.OrdinalIgnoreCase) { "CW" };

    public static OneSixtyMeterScoreBreakdown Score(
        IEnumerable<Qso> qsos,
        IReadOnlyDictionary<string, DxccEntity?> myEntityByStationCallsign)
    {
        int qsoCount = 0;
        int qsoPoints = 0;
        int unresolvedCount = 0;
        var multiplierKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var qso in qsos)
        {
            if (string.IsNullOrWhiteSpace(qso.Band) || !ScoringBands.Contains(qso.Band) || !CwModes.Contains(qso.Mode))
                continue;

            bool myEntityKnown = myEntityByStationCallsign.TryGetValue(qso.StationCallsign, out var myEntity) && myEntity is not null;
            if (!myEntityKnown || qso.DxccEntityCode is null)
            {
                unresolvedCount++;
                continue;
            }

            bool amWve = IsWve(myEntity!.EntityCode);
            bool workedIsWve = IsWve(qso.DxccEntityCode.Value);

            if (!amWve && !workedIsWve)
            {
                unresolvedCount++; // DX-to-DX doesn't count, rule 2.2
                continue;
            }

            qsoCount++;
            qsoPoints += (amWve && workedIsWve) ? WveToWvePoints : CrossBoundaryPoints;

            if (amWve)
            {
                if (workedIsWve)
                {
                    string? section = qso.ArrlSection?.Trim();
                    if (!string.IsNullOrEmpty(section))
                        multiplierKeys.Add("SEC:" + section.ToUpperInvariant());
                }
                else
                {
                    multiplierKeys.Add("ENT:" + qso.DxccEntityCode.Value);
                }
            }
            else
            {
                string? section = qso.ArrlSection?.Trim();
                if (!string.IsNullOrEmpty(section))
                    multiplierKeys.Add("SEC:" + section.ToUpperInvariant());
            }
        }

        int multiplier = multiplierKeys.Count;
        return new OneSixtyMeterScoreBreakdown(qsoCount, qsoPoints, multiplier, qsoPoints * multiplier, unresolvedCount);
    }

    private static bool IsWve(int entityCode) => entityCode == UsaEntityCode || entityCode == CanadaEntityCode;
}
