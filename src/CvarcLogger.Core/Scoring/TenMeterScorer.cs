using CvarcLogger.Core.Models;

namespace CvarcLogger.Core.Scoring;

public record TenMeterScoreBreakdown(int QsoCount, int PhoneQsos, int CwQsos, int QsoPoints, int PhoneMultiplier, int CwMultiplier, int Score);

/// <summary>Scoring for the ARRL 10-Meter Contest: single band (28 MHz), any station may contact any
/// other, worked once per mode (so the same station can be worked again on the other mode). Phone
/// contacts are 2 QSO points, CW contacts are 4. The multiplier is US states/DC/Canadian provinces
/// (Hawaii/Alaska count as US states here, unlike the DX Contest) plus Mexican states plus DXCC entities,
/// counted once on Phone and once on CW (two separate pools, since there's only the one band to begin
/// with). ITU-region multipliers for maritime/aeronautical mobile stations aren't tracked -- too niche a
/// case to be worth a dedicated field. See https://contests.arrl.org/ContestRules/10-Meter-Rules.pdf.</summary>
public static class TenMeterScorer
{
    private const int UsaEntityCode = 291;
    private const int CanadaEntityCode = 1;
    private const int MexicoEntityCode = 50;
    private const int PhonePointsPerQso = 2;
    private const int CwPointsPerQso = 4;

    public static readonly IReadOnlySet<string> ScoringBands = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "10m" };

    private static readonly HashSet<string> PhoneModes = new(StringComparer.OrdinalIgnoreCase) { "SSB", "USB", "LSB", "AM", "FM", "PH" };
    private static readonly HashSet<string> CwModes = new(StringComparer.OrdinalIgnoreCase) { "CW" };

    public static TenMeterScoreBreakdown Score(IEnumerable<Qso> qsos)
    {
        int phoneQsos = 0, cwQsos = 0;
        var phoneMult = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cwMult = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var qso in qsos)
        {
            if (string.IsNullOrWhiteSpace(qso.Band) || !ScoringBands.Contains(qso.Band))
                continue;

            bool isPhone = PhoneModes.Contains(qso.Mode);
            bool isCw = !isPhone && CwModes.Contains(qso.Mode);
            if (!isPhone && !isCw)
                continue;

            if (isPhone) phoneQsos++; else cwQsos++;

            string? key = MultiplierKeyFor(qso);
            if (key is not null)
                (isPhone ? phoneMult : cwMult).Add(key);
        }

        int qsoPoints = phoneQsos * PhonePointsPerQso + cwQsos * CwPointsPerQso;
        int multiplier = phoneMult.Count + cwMult.Count;
        return new TenMeterScoreBreakdown(phoneQsos + cwQsos, phoneQsos, cwQsos, qsoPoints, phoneMult.Count, cwMult.Count, qsoPoints * multiplier);
    }

    private static string? MultiplierKeyFor(Qso qso)
    {
        string? state = qso.State?.Trim();

        if (qso.DxccEntityCode == UsaEntityCode || qso.DxccEntityCode == CanadaEntityCode)
            return string.IsNullOrEmpty(state) ? null : "SP:" + state.ToUpperInvariant();

        if (qso.DxccEntityCode == MexicoEntityCode)
            return string.IsNullOrEmpty(state) ? null : "MEX:" + state.ToUpperInvariant();

        return qso.DxccEntityCode is int code ? "ENT:" + code : null;
    }
}
