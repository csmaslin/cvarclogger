using CommunityToolkit.Mvvm.ComponentModel;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Scoring;

namespace CvarcLogger.App.ViewModels;

/// <summary>Tracks NAQP (North American QSO Party) scoring for CW, SSB, and RTTY -- each run twice a
/// year on a fixed weekend-of-month schedule (NCJ rule 4), 1800 UTC Saturday through 0559:59 UTC Sunday.
/// Score = total valid QSOs x multipliers, with multipliers (US states/DC/Canadian provinces/other NA
/// countries) counted again on each band (rule 11) -- unlike the Sprints tab's once-per-event multiplier,
/// hence the shared SprintScorer.Score's multiplierPerBand flag. Bands are 160-10m for CW/SSB and 80-10m
/// for RTTY (no 160m per rule 8).</summary>
public partial class NaqpViewModel : ObservableObject
{
    private readonly IQsoRepository _qsoRepository;

    private static readonly HashSet<string> CwSsbBands = new(StringComparer.OrdinalIgnoreCase) { "160m", "80m", "40m", "20m", "15m", "10m" };
    private static readonly HashSet<string> RttyBands = new(StringComparer.OrdinalIgnoreCase) { "80m", "40m", "20m", "15m", "10m" };

    private static readonly HashSet<string> CwModes = new(StringComparer.OrdinalIgnoreCase) { "CW" };
    private static readonly HashSet<string> SsbModes = new(StringComparer.OrdinalIgnoreCase) { "SSB", "USB", "LSB", "AM", "FM", "PH" };
    private static readonly HashSet<string> RttyModes = new(StringComparer.OrdinalIgnoreCase) { "RTTY" };

    [ObservableProperty] private int cwJanQsoCount;
    [ObservableProperty] private int cwJanMultiplier;
    [ObservableProperty] private int cwJanScore;

    [ObservableProperty] private int cwAugQsoCount;
    [ObservableProperty] private int cwAugMultiplier;
    [ObservableProperty] private int cwAugScore;

    [ObservableProperty] private int ssbJanQsoCount;
    [ObservableProperty] private int ssbJanMultiplier;
    [ObservableProperty] private int ssbJanScore;

    [ObservableProperty] private int ssbAugQsoCount;
    [ObservableProperty] private int ssbAugMultiplier;
    [ObservableProperty] private int ssbAugScore;

    [ObservableProperty] private int rttyFebQsoCount;
    [ObservableProperty] private int rttyFebMultiplier;
    [ObservableProperty] private int rttyFebScore;

    [ObservableProperty] private int rttyJulQsoCount;
    [ObservableProperty] private int rttyJulMultiplier;
    [ObservableProperty] private int rttyJulScore;

    [ObservableProperty] private int totalQsoCount;
    [ObservableProperty] private int totalScore;

    public NaqpViewModel(IQsoRepository qsoRepository)
    {
        _qsoRepository = qsoRepository;
    }

    public async Task LoadAsync(int year)
    {
        var allQsos = await _qsoRepository.GetAllAsync();

        // CW: 2nd full weekend January, 1st full weekend August.
        var cwJan = Score(allQsos, GetNthWeekend(year, 1, 2), CwModes, CwSsbBands);
        CwJanQsoCount = cwJan.QsoCount; CwJanMultiplier = cwJan.Multiplier; CwJanScore = cwJan.Score;

        var cwAug = Score(allQsos, GetNthWeekend(year, 8, 1), CwModes, CwSsbBands);
        CwAugQsoCount = cwAug.QsoCount; CwAugMultiplier = cwAug.Multiplier; CwAugScore = cwAug.Score;

        // SSB: 3rd full weekend January, 3rd full weekend August.
        var ssbJan = Score(allQsos, GetNthWeekend(year, 1, 3), SsbModes, CwSsbBands);
        SsbJanQsoCount = ssbJan.QsoCount; SsbJanMultiplier = ssbJan.Multiplier; SsbJanScore = ssbJan.Score;

        var ssbAug = Score(allQsos, GetNthWeekend(year, 8, 3), SsbModes, CwSsbBands);
        SsbAugQsoCount = ssbAug.QsoCount; SsbAugMultiplier = ssbAug.Multiplier; SsbAugScore = ssbAug.Score;

        // RTTY: last Saturday in February, 3rd full weekend July. No 160m.
        var rttyFeb = Score(allQsos, GetLastWeekend(year, 2), RttyModes, RttyBands);
        RttyFebQsoCount = rttyFeb.QsoCount; RttyFebMultiplier = rttyFeb.Multiplier; RttyFebScore = rttyFeb.Score;

        var rttyJul = Score(allQsos, GetNthWeekend(year, 7, 3), RttyModes, RttyBands);
        RttyJulQsoCount = rttyJul.QsoCount; RttyJulMultiplier = rttyJul.Multiplier; RttyJulScore = rttyJul.Score;

        TotalQsoCount = cwJan.QsoCount + cwAug.QsoCount + ssbJan.QsoCount + ssbAug.QsoCount + rttyFeb.QsoCount + rttyJul.QsoCount;
        TotalScore = cwJan.Score + cwAug.Score + ssbJan.Score + ssbAug.Score + rttyFeb.Score + rttyJul.Score;
    }

    private static SprintScoreBreakdown Score(List<Core.Models.Qso> allQsos, (DateTime start, DateTime end) window, IReadOnlySet<string> modes, IReadOnlySet<string> bands)
    {
        var eventQsos = allQsos.Where(q =>
            q.QsoDateTimeOnUtc >= window.start && q.QsoDateTimeOnUtc <= window.end &&
            modes.Contains(q.Mode));
        return SprintScorer.Score(eventQsos, bands, multiplierPerBand: true);
    }

    /// <summary>The Nth Saturday of the month (1800 UTC) through the following Sunday 05:59:59 UTC.</summary>
    private static (DateTime start, DateTime end) GetNthWeekend(int year, int month, int n)
    {
        var firstOfMonth = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var firstSaturday = firstOfMonth.AddDays((6 - (int)firstOfMonth.DayOfWeek + 7) % 7);
        var saturday = firstSaturday.AddDays((n - 1) * 7);

        var start = saturday.AddHours(18);
        var end = saturday.AddDays(1).AddHours(5).AddMinutes(59).AddSeconds(59);
        return (start, end);
    }

    /// <summary>The last Saturday of the month (1800 UTC) through the following Sunday 05:59:59 UTC --
    /// used for the RTTY February event, which NCJ defines as "last Saturday in February" rather than an
    /// Nth-full-weekend rule.</summary>
    private static (DateTime start, DateTime end) GetLastWeekend(int year, int month)
    {
        var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month), 0, 0, 0, DateTimeKind.Utc);
        while (lastDay.DayOfWeek != DayOfWeek.Saturday)
            lastDay = lastDay.AddDays(-1);

        var start = lastDay.AddHours(18);
        var end = lastDay.AddDays(1).AddHours(5).AddMinutes(59).AddSeconds(59);
        return (start, end);
    }
}
