using CvarcLogger.Core.Models;

namespace CvarcLogger.Core.Scoring;

public record VhfContestScoreBreakdown(int QsoCount, int QsoPoints, int GridSquareMultiplier, int Score);

/// <summary>Scoring for the ARRL January/June/September VHF Contest (fixed-station category), per
/// https://contests.arrl.org/ContestRules/JanJunSep-VHF-Rules.pdf section 5: QSO points depend on band
/// (50/144 MHz = 1, 222/432 MHz = 2, 902/1296 MHz = 4 in January or 3 in June/September, 2.3 GHz and up =
/// 8 in January or 4 in June/September); the multiplier is the number of distinct 4-character grid
/// squares worked, counted once per band and summed across bands (rule 5.3.1); final score for fixed
/// stations = total QSO points x total multipliers (rule 5.4.1). All modes count, unlike the CW/SSB/RTTY
/// Sprints -- there's no per-mode filtering here. Rover scoring (rule 5.4.2) is a different formula and
/// is not implemented.</summary>
public static class VhfContestScorer
{
    private static readonly HashSet<string> Tier1Bands = new(StringComparer.OrdinalIgnoreCase) { "6m", "2m" };
    private static readonly HashSet<string> Tier2Bands = new(StringComparer.OrdinalIgnoreCase) { "1.25m", "70cm" };
    private static readonly HashSet<string> Tier3Bands = new(StringComparer.OrdinalIgnoreCase) { "33cm", "23cm" };
    private static readonly HashSet<string> Tier4Bands = new(StringComparer.OrdinalIgnoreCase)
    {
        "13cm", "9cm", "6cm", "3cm", "1.25cm", "6mm", "4mm", "2.5mm", "2mm", "1mm",
    };

    public static VhfContestScoreBreakdown Score(IEnumerable<Qso> qsos, int tier3Points, int tier4Points)
    {
        int qsoCount = 0;
        int qsoPoints = 0;
        var gridsByBand = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var qso in qsos)
        {
            if (string.IsNullOrWhiteSpace(qso.Band))
                continue;

            int? points = PointsFor(qso.Band, tier3Points, tier4Points);
            if (points is null)
                continue; // not a 50 MHz-and-up band this scorer recognizes.

            qsoCount++;
            qsoPoints += points.Value;

            string? grid4 = NormalizeGrid(qso.GridSquare);
            if (grid4 is not null)
            {
                if (!gridsByBand.TryGetValue(qso.Band, out var grids))
                    gridsByBand[qso.Band] = grids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                grids.Add(grid4);
            }
        }

        int multiplier = gridsByBand.Values.Sum(set => set.Count);
        return new VhfContestScoreBreakdown(qsoCount, qsoPoints, multiplier, qsoPoints * multiplier);
    }

    private static int? PointsFor(string band, int tier3Points, int tier4Points)
    {
        if (Tier1Bands.Contains(band)) return 1;
        if (Tier2Bands.Contains(band)) return 2;
        if (Tier3Bands.Contains(band)) return tier3Points;
        if (Tier4Bands.Contains(band)) return tier4Points;
        return null;
    }

    private static string? NormalizeGrid(string? gridSquare)
    {
        string? trimmed = gridSquare?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length < 4)
            return null;

        return trimmed.Substring(0, 4).ToUpperInvariant();
    }
}
