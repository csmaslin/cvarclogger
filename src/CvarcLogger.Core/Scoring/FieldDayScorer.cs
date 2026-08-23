using CvarcLogger.Core.Models;

namespace CvarcLogger.Core.Scoring;

/// <summary>Computes ARRL Field Day scoring for a set of QSOs. Rules per the 2024 ARRL Field Day
/// rules packet:
///
///   QSO points:
///     - Phone (SSB, FM, AM)          = 1 pt each
///     - CW                            = 2 pts each
///     - Digital (RTTY, FT8, PSK, ...) = 2 pts each
///
///   Power multiplier (applied to QSO points, NOT to bonuses):
///     - QRP battery-only (5W or less)                        = ×5
///     - 150W or less                                          = ×2
///     - Over 150W                                             = ×1
///
///   Sections worked: ARRL/RAC section from each QSO's exchange is counted once for the
///     "sections worked" tally. Not a score multiplier in FD -- tracked for the operator's
///     "clean sweep" awareness and per the results write-up.
///
///   Final Score = (QSO points × power multiplier) + bonus points
///
/// Bonuses (Copperton to Sat, emergency power declaration, message to SM/SEC, W1AW bulletin,
/// alternate power source, satellite QSO, educational activity, etc.) are inputs on the score
/// call rather than derived from the log -- most are all-or-nothing declarations the operator
/// makes, not something detectable from QSO records.</summary>
public enum FieldDayPowerClass
{
    /// <summary>Battery/solar only, 5W or less. Multiplier ×5.</summary>
    QrpBattery,
    /// <summary>150W or less (commercial power OK). Multiplier ×2.</summary>
    LowPower,
    /// <summary>Over 150W. Multiplier ×1.</summary>
    HighPower,
}

public record FieldDayScoreBreakdown(
    int TotalQsos,
    int PhoneQsos,
    int CwQsos,
    int DigitalQsos,
    int PhonePoints,
    int CwPoints,
    int DigitalPoints,
    int RawQsoPoints,
    int PowerMultiplier,
    int MultipliedQsoPoints,
    int BonusPoints,
    int FinalScore,
    IReadOnlyList<string> SectionsWorked);

public static class FieldDayScorer
{
    /// <summary>Field Day QSO-point values by mode category, from the ARRL FD rules.</summary>
    private const int PhonePointsPerQso = 1;
    private const int CwPointsPerQso = 2;
    private const int DigitalPointsPerQso = 2;

    public static FieldDayScoreBreakdown Score(
        IEnumerable<Qso> qsos,
        FieldDayPowerClass powerClass,
        int bonusPoints = 0)
    {
        int phone = 0, cw = 0, digital = 0;
        var sections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var q in qsos)
        {
            switch (ClassifyMode(q.Mode, q.SubMode))
            {
                case ModeCategory.Phone: phone++; break;
                case ModeCategory.Cw: cw++; break;
                case ModeCategory.Digital: digital++; break;
            }

            // FD exchange is "<class> <section>" (e.g. "3A CO"). Reader stores the section in ArrlSection
            // when it recognizes it, otherwise State, otherwise Class -- prefer them in that order.
            string? section = FirstNonBlank(q.ArrlSection, q.State);
            if (!string.IsNullOrEmpty(section)) sections.Add(section);
        }

        int phonePts = phone * PhonePointsPerQso;
        int cwPts = cw * CwPointsPerQso;
        int digitalPts = digital * DigitalPointsPerQso;
        int rawPts = phonePts + cwPts + digitalPts;

        int mult = PowerMultiplierFor(powerClass);
        int multipliedPts = rawPts * mult;
        int final = multipliedPts + bonusPoints;

        // Materialize sections in a stable alphabetical order so the same log always produces the same
        // report -- HashSet iteration order isn't guaranteed and can change between .NET versions.
        var sortedSections = sections.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();

        return new FieldDayScoreBreakdown(
            TotalQsos: phone + cw + digital,
            PhoneQsos: phone,
            CwQsos: cw,
            DigitalQsos: digital,
            PhonePoints: phonePts,
            CwPoints: cwPts,
            DigitalPoints: digitalPts,
            RawQsoPoints: rawPts,
            PowerMultiplier: mult,
            MultipliedQsoPoints: multipliedPts,
            BonusPoints: bonusPoints,
            FinalScore: final,
            SectionsWorked: sortedSections);
    }

    private static int PowerMultiplierFor(FieldDayPowerClass power) => power switch
    {
        FieldDayPowerClass.QrpBattery => 5,
        FieldDayPowerClass.LowPower => 2,
        FieldDayPowerClass.HighPower => 1,
        _ => 1,
    };

    private enum ModeCategory { Phone, Cw, Digital, Unknown }

    /// <summary>Maps a QSO's Mode/SubMode into the three Field Day categories. Anything unrecognized
    /// falls into Unknown (which contributes zero points) rather than defaulting to Phone -- silently
    /// scoring an unknown mode at 1 pt could inflate the total without anyone noticing.</summary>
    private static ModeCategory ClassifyMode(string mode, string? subMode)
    {
        string m = (mode ?? string.Empty).Trim().ToUpperInvariant();
        string s = (subMode ?? string.Empty).Trim().ToUpperInvariant();

        return m switch
        {
            "CW" => ModeCategory.Cw,
            "SSB" or "USB" or "LSB" or "AM" or "FM" or "PH" or "PHONE" => ModeCategory.Phone,
            "RTTY" or "PSK" or "PSK31" or "FT8" or "FT4" or "JT65" or "JT9" or "MFSK" or "DIGITAL" or "DIGI" or "DATA" or "DG" => ModeCategory.Digital,
            _ => s switch
            {
                "USB" or "LSB" or "AM" or "FM" => ModeCategory.Phone,
                "FT8" or "FT4" or "PSK31" or "RTTY" or "JT65" => ModeCategory.Digital,
                _ => ModeCategory.Unknown,
            }
        };
    }

    private static string? FirstNonBlank(params string?[] candidates)
    {
        foreach (var c in candidates)
            if (!string.IsNullOrWhiteSpace(c)) return c!.Trim().ToUpperInvariant();
        return null;
    }
}
