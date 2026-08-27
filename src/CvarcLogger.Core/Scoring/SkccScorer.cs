using CvarcLogger.Core.Models;

namespace CvarcLogger.Core.Scoring;

/// <summary>Computes SKCC (Straight Key Century Club) event scoring for a set of QSOs. Rules fetched
/// directly from the club's own event pages (not the speculative placeholder formula the original
/// implementation plan carried before verification):
///   - skccgroup.com/operating_activities/weekday_sprint/  (SKS)
///   - skccgroup.com/operating_activities/weekend_sprintathon/  (WES)
///   - skccgroup.com/operating_activities/skse/  (Europe Sprint)
///   - skccgroup.com/operating_activities/sksa/  (Asia Sprint -- NOT South America, see SkccEventType)
///   - skccgroup.com/operating_activities/QSO_Party/
///
/// All five share one core formula:
///
///   QSO points:   1 point per unique (callsign, band) pair -- the same station on a different band
///                 scores again, but a repeat on the same band does not. Not tier-scaled (an earlier
///                 draft of this plan assumed member/non-member had different QSO point values; the
///                 official rules don't -- the tier value comes entirely from the bonus below).
///
///   Multipliers:  1 point per unique SPC (US State / Canadian Province / DXCC Country) worked, counted
///                 once for the whole event regardless of how many bands/QSOs touched it.
///
///   Tier bonus:   per unique member worked (once per event, regardless of band): Centurion (C) = 5,
///                 Tribune (T) = 10, Senator (S) = 15. If the same callsign's tier suffix changed during
///                 the log (upgraded mid-event), the highest tier seen is used -- tiers only ever
///                 increase, never downgrade.
///
///   Final score:  (QSO points x Multipliers) + Bonus points
///
/// Event-specific extra bonuses, where confirmed and mechanically derivable from the log:
///   - WES:        +25 per band worked against the special sprint station KS1KCC.
///   - Asia (SKSA): +30 per unique "/BD" (birthday) station worked.
///   - QSO Party:  +5 per unique 4-character grid square worked, +25 per band against KS1KCC.
///
/// Two bonuses are genuinely NOT derivable from the log and must be supplied by the operator:
///   - SKS's "Designated Special SKCC Member" varies sprint to sprint -- pass its callsign via
///     specialMemberCallsign to score the +25/band bonus for it.
///   - QSO Party's +100 photo/soapbox bonus is a manual submission choice, not a QSO fact -- pass
///     soapboxPhotoSubmitted: true to include it.
///
/// SouthAmericaSprint and SlowSpeedSaunter have no confirmed official scoring rules and are not
/// supported -- Score() throws for them rather than guessing.</summary>
public record SkccMultiplierEntry(string Spc, int QsoCount);

public record SkccMemberBonusEntry(string Callsign, char Tier, int Points);

/// <summary>One bonus line item, mirroring FieldDayBonusItem's Name/Points shape -- used both for the
/// bonuses this scorer derives itself (KS1KCC, grid squares, /BD stations) and for the two genuinely
/// manual ones the caller supplies.</summary>
public record SkccBonusItem(string Description, int Points);

public record SkccScoreBreakdown(
    SkccEventType EventType,
    int TotalQsos,
    int TotalQsoPoints,
    int TotalMultipliers,
    IReadOnlyList<SkccMultiplierEntry> MultiplierDetails,
    int TierBonusPoints,
    IReadOnlyList<SkccMemberBonusEntry> TierBonusDetails,
    int ExtraBonusPoints,
    IReadOnlyList<SkccBonusItem> ExtraBonuses,
    int BonusPoints,
    int FinalScore);

public static class SkccScorer
{
    private const string Ks1kccCallsign = "KS1KCC";
    private const int Ks1kccBonusPerBand = 25;
    private const int GridSquareBonusPoints = 5;
    private const int BirthdayStationBonusPoints = 30;
    private const int SpecialMemberBonusPerBand = 25;
    private const int SoapboxPhotoBonusPoints = 100;

    public static bool IsEventSupported(SkccEventType eventType) => eventType is
        SkccEventType.WeekdaySprint or SkccEventType.WeekendSprintathon or
        SkccEventType.EuropeSprint or SkccEventType.AsiaSprint or SkccEventType.QsoParty;

    public static SkccScoreBreakdown Score(
        IEnumerable<Qso> qsos,
        SkccEventType eventType,
        string? specialMemberCallsign = null,
        bool soapboxPhotoSubmitted = false)
    {
        if (!IsEventSupported(eventType))
            throw new NotSupportedException(
                $"There are no confirmed official SKCC scoring rules for {eventType} yet -- refusing to guess. " +
                "Supported events: Weekday Sprint, Weekend Sprintathon, Europe Sprint, Asia Sprint, QSO Party.");

        var list = qsos.ToList();

        var qsoPointPairs = new HashSet<(string Call, string Band)>();
        foreach (var q in list)
            qsoPointPairs.Add((Normalize(q.Callsign), (q.Band ?? "").Trim().ToUpperInvariant()));
        int qsoPoints = qsoPointPairs.Count;

        var spcCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var q in list)
        {
            string? spc = FirstNonBlank(q.State, q.Country);
            if (string.IsNullOrWhiteSpace(spc)) continue;
            spc = spc.Trim().ToUpperInvariant();
            spcCounts[spc] = spcCounts.GetValueOrDefault(spc) + 1;
        }
        var multiplierDetails = spcCounts
            .Select(kv => new SkccMultiplierEntry(kv.Key, kv.Value))
            .OrderBy(m => m.Spc, StringComparer.OrdinalIgnoreCase)
            .ToList();
        int multipliers = multiplierDetails.Count;

        var tierByCall = new Dictionary<string, char>(StringComparer.OrdinalIgnoreCase);
        foreach (var q in list)
        {
            char? tier = TierOf(q.SkccNr);
            if (tier is null) continue;
            string call = Normalize(q.Callsign);
            if (!tierByCall.TryGetValue(call, out var existing) || TierRank(tier.Value) > TierRank(existing))
                tierByCall[call] = tier.Value;
        }
        var tierBonusDetails = tierByCall
            .Select(kv => new SkccMemberBonusEntry(kv.Key, kv.Value, TierBonusPointsFor(kv.Value)))
            .OrderBy(t => t.Callsign, StringComparer.OrdinalIgnoreCase)
            .ToList();
        int tierBonusPoints = tierBonusDetails.Sum(t => t.Points);

        var extras = new List<SkccBonusItem>();
        switch (eventType)
        {
            case SkccEventType.WeekendSprintathon:
                extras.AddRange(Ks1kccPerBandBonus(list));
                break;
            case SkccEventType.AsiaSprint:
                extras.AddRange(BirthdayStationBonus(list));
                break;
            case SkccEventType.QsoParty:
                extras.AddRange(GridSquareBonus(list));
                extras.AddRange(Ks1kccPerBandBonus(list));
                if (soapboxPhotoSubmitted)
                    extras.Add(new SkccBonusItem("Photo/soapbox submitted", SoapboxPhotoBonusPoints));
                break;
            case SkccEventType.WeekdaySprint:
                if (!string.IsNullOrWhiteSpace(specialMemberCallsign))
                    extras.AddRange(SpecialMemberPerBandBonus(list, specialMemberCallsign));
                break;
            // EuropeSprint: no confirmed extra bonuses beyond the tier bonus above.
        }

        int extraBonusPoints = extras.Sum(b => b.Points);
        int bonusPoints = tierBonusPoints + extraBonusPoints;
        int finalScore = (qsoPoints * multipliers) + bonusPoints;

        return new SkccScoreBreakdown(
            EventType: eventType,
            TotalQsos: list.Count,
            TotalQsoPoints: qsoPoints,
            TotalMultipliers: multipliers,
            MultiplierDetails: multiplierDetails,
            TierBonusPoints: tierBonusPoints,
            TierBonusDetails: tierBonusDetails,
            ExtraBonusPoints: extraBonusPoints,
            ExtraBonuses: extras,
            BonusPoints: bonusPoints,
            FinalScore: finalScore);
    }

    private static string Normalize(string? call) => (call ?? "").Trim().ToUpperInvariant();

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>SKCC numbers carry an award-level suffix once earned: "1234C" = Centurion, "1234T" =
    /// Tribune, "1234S" = Senator, no suffix (or "NONE") = non-member/plain, which earns no tier bonus.</summary>
    private static char? TierOf(string? skccNr)
    {
        string nr = (skccNr ?? "").Trim().ToUpperInvariant();
        if (nr.Length == 0 || nr == "NONE") return null;
        char last = nr[^1];
        return last is 'C' or 'T' or 'S' ? last : null;
    }

    private static int TierRank(char tier) => tier switch { 'C' => 1, 'T' => 2, 'S' => 3, _ => 0 };

    private static int TierBonusPointsFor(char tier) => tier switch { 'C' => 5, 'T' => 10, 'S' => 15, _ => 0 };

    private static IEnumerable<SkccBonusItem> Ks1kccPerBandBonus(IReadOnlyList<Qso> qsos) =>
        BandsWorkedAgainst(qsos, Ks1kccCallsign)
            .Select(band => new SkccBonusItem($"KS1KCC contacted on {band}", Ks1kccBonusPerBand));

    private static IEnumerable<SkccBonusItem> SpecialMemberPerBandBonus(IReadOnlyList<Qso> qsos, string callsign) =>
        BandsWorkedAgainst(qsos, callsign)
            .Select(band => new SkccBonusItem($"Designated Special Member ({Normalize(callsign)}) contacted on {band}", SpecialMemberBonusPerBand));

    private static IEnumerable<string> BandsWorkedAgainst(IReadOnlyList<Qso> qsos, string callsign)
    {
        string target = Normalize(callsign);
        return qsos
            .Where(q => Normalize(q.Callsign) == target)
            .Select(q => (q.Band ?? "").Trim().ToUpperInvariant())
            .Where(b => b.Length > 0)
            .Distinct()
            .OrderBy(b => b, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<SkccBonusItem> BirthdayStationBonus(IReadOnlyList<Qso> qsos) =>
        qsos
            .Select(q => Normalize(q.Callsign))
            .Where(c => c.EndsWith("/BD", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .Select(c => new SkccBonusItem($"Birthday station {c}", BirthdayStationBonusPoints));

    private static IEnumerable<SkccBonusItem> GridSquareBonus(IReadOnlyList<Qso> qsos) =>
        qsos
            .Select(q => (q.GridSquare ?? "").Trim().ToUpperInvariant())
            .Where(g => g.Length >= 4)
            .Select(g => g[..4])
            .Distinct()
            .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
            .Select(g => new SkccBonusItem($"Grid square {g}", GridSquareBonusPoints));
}
