using CvarcLogger.Core.Models;

namespace CvarcLogger.Core.Scoring;

/// <summary>Selects the QSOs that count toward a specific Field Day event from the full log. Field Day
/// runs the fourth full weekend of June, 1800 UTC Saturday through 2100 UTC Sunday -- this filter
/// enforces the exact 27-hour window plus a contest-id tag when the log has one, so a mid-year
/// re-export always picks up the same set of QSOs regardless of what's happened in the log since.</summary>
public static class FieldDayQsoFilter
{
    /// <summary>Returns the (start, end) UTC window for ARRL Field Day in the given year -- fourth full
    /// weekend of June, Saturday 1800Z through Sunday 2100Z per ARRL rules.</summary>
    public static (DateTime StartUtc, DateTime EndUtc) WindowFor(int year)
    {
        // Fourth full weekend of June: first Saturday whose Sunday is also in June, offset by 3 weeks.
        var june1 = new DateTime(year, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        int daysToFirstSaturday = ((int)DayOfWeek.Saturday - (int)june1.DayOfWeek + 7) % 7;
        var firstSaturday = june1.AddDays(daysToFirstSaturday);
        var fourthSaturday = firstSaturday.AddDays(21);

        var start = new DateTime(fourthSaturday.Year, fourthSaturday.Month, fourthSaturday.Day, 18, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(27); // Sat 1800Z + 27h = Sun 2100Z
        return (start, end);
    }

    /// <summary>QSOs made inside the given year's Field Day window. Optionally requires the QSO to
    /// carry a matching ContestId so mixed logs (regular ops + FD in the same weekend) don't accidentally
    /// score non-contest contacts.</summary>
    public static IEnumerable<Qso> ForYear(IEnumerable<Qso> qsos, int year, string? requiredContestId = "ARRL-FIELD-DAY")
    {
        var (start, end) = WindowFor(year);
        foreach (var q in qsos)
        {
            if (q.QsoDateTimeOnUtc < start || q.QsoDateTimeOnUtc > end) continue;
            if (!string.IsNullOrEmpty(requiredContestId) &&
                !string.Equals(q.ContestId, requiredContestId, StringComparison.OrdinalIgnoreCase))
                continue;
            yield return q;
        }
    }
}
