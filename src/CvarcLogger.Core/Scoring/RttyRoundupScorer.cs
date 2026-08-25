using CvarcLogger.Core.Models;

namespace CvarcLogger.Core.Scoring;

public record RttyRoundupScoreBreakdown(int QsoCount, int Multiplier, int Score);

/// <summary>Scoring for the ARRL RTTY Roundup: bands 80/40/20/15/10m, RTTY mode only, worked once per
/// band. Every contact is a flat 1 QSO point. The multiplier is DXCC entities (except USA and Canada,
/// whose individual states/provinces count instead) plus US states/DC/Canadian provinces -- counted
/// once for the whole contest, not once per band (unlike most other contests here). Hawaii/Alaska count
/// as DXCC entities, not states. See https://contests.arrl.org/ContestRules/RTTY-RU-Rules.pdf.</summary>
public static class RttyRoundupScorer
{
    private const int UsaEntityCode = 291;
    private const int CanadaEntityCode = 1;

    public static readonly IReadOnlySet<string> ScoringBands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "80m", "40m", "20m", "15m", "10m",
    };

    private static readonly HashSet<string> RttyModes = new(StringComparer.OrdinalIgnoreCase) { "RTTY" };

    public static RttyRoundupScoreBreakdown Score(IEnumerable<Qso> qsos)
    {
        int qsoCount = 0;
        var multiplierKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var qso in qsos)
        {
            if (string.IsNullOrWhiteSpace(qso.Band) || !ScoringBands.Contains(qso.Band) || !RttyModes.Contains(qso.Mode))
                continue;

            qsoCount++;

            string? key = MultiplierKeyFor(qso);
            if (key is not null)
                multiplierKeys.Add(key);
        }

        int multiplier = multiplierKeys.Count;
        return new RttyRoundupScoreBreakdown(qsoCount, multiplier, qsoCount * multiplier);
    }

    private static string? MultiplierKeyFor(Qso qso)
    {
        string? state = qso.State?.Trim();

        if (qso.DxccEntityCode == UsaEntityCode || qso.DxccEntityCode == CanadaEntityCode)
            return string.IsNullOrEmpty(state) ? null : "SP:" + state.ToUpperInvariant();

        return qso.DxccEntityCode is int code ? "ENT:" + code : null;
    }
}
