using CommunityToolkit.Mvvm.ComponentModel;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Models;

namespace CvarcLogger.App.ViewModels;

/// <summary>Tracks ARRL Sweepstakes scoring for both CW (first full weekend of November) and
/// Phone (third full weekend of November) events. Each event runs Saturday 21:00 UTC through
/// Monday 02:59 UTC. Score = Total QSOs × Unique Sections (max 85).</summary>
public partial class SweepstakesViewModel : ObservableObject
{
    private readonly IQsoRepository _qsoRepository;

    [ObservableProperty] private int cwQsoCount;
    [ObservableProperty] private int cwSectionCount;
    [ObservableProperty] private int cwScore;

    [ObservableProperty] private int phoneQsoCount;
    [ObservableProperty] private int phoneSectionCount;
    [ObservableProperty] private int phoneScore;

    [ObservableProperty] private int totalQsoCount;
    [ObservableProperty] private int totalSectionCount;
    [ObservableProperty] private int totalScore;

    public SweepstakesViewModel(IQsoRepository qsoRepository)
    {
        _qsoRepository = qsoRepository;
    }

    public async Task LoadAsync(int year)
    {
        var allQsos = await _qsoRepository.GetAllAsync();

        // CW event: first full weekend of November
        (var cwQsos, var cwSections) = CalculateSweepstakesScore(allQsos, year, 1, "CW");
        CwQsoCount = cwQsos;
        CwSectionCount = cwSections;
        CwScore = cwQsos * cwSections;

        // Phone event: third full weekend of November
        (var phoneQsos, var phoneSections) = CalculateSweepstakesScore(allQsos, year, 3, "SSB", "AM", "FM");
        PhoneQsoCount = phoneQsos;
        PhoneSectionCount = phoneSections;
        PhoneScore = phoneQsos * phoneSections;

        // Totals
        TotalQsoCount = cwQsos + phoneQsos;
        TotalSectionCount = Math.Min(cwSections + phoneSections, 85);
        TotalScore = CwScore + PhoneScore;
    }

    private (int qsoCount, int sectionCount) CalculateSweepstakesScore(List<Qso> allQsos, int year, int weekendNumber, params string[] modes)
    {
        var eventDates = GetSweepstakesEventDates(year, weekendNumber);
        var startUtc = eventDates.start;
        var endUtc = eventDates.end;

        var eventQsos = allQsos
            .Where(q => q.QsoDateTimeOnUtc >= startUtc &&
                        q.QsoDateTimeOnUtc <= endUtc &&
                        modes.Contains(q.Mode, StringComparer.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(q.ArrlSection))
            .ToList();

        int qsoCount = eventQsos.Count;
        int sectionCount = eventQsos
            .Select(q => q.ArrlSection)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return (qsoCount, Math.Min(sectionCount, 85));
    }

    private (DateTime start, DateTime end) GetSweepstakesEventDates(int year, int weekendNumber)
    {
        // Find the first full weekend (Saturday-Monday) of November
        var nov1 = new DateTime(year, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var firstSaturday = nov1.AddDays((6 - (int)nov1.DayOfWeek) % 7);

        if (weekendNumber == 1)
        {
            // First full weekend: Sat 21:00 UTC - Mon 02:59 UTC
            var startSaturday = firstSaturday;
            if (startSaturday < nov1) startSaturday = startSaturday.AddDays(7);

            var start = startSaturday.AddHours(21);
            var end = startSaturday.AddDays(2).AddHours(2).AddMinutes(59);
            return (start, end);
        }
        else
        {
            // Third full weekend: Sat 21:00 UTC - Mon 02:59 UTC (two weeks later)
            var startSaturday = firstSaturday.AddDays(14);
            var start = startSaturday.AddHours(21);
            var end = startSaturday.AddDays(2).AddHours(2).AddMinutes(59);
            return (start, end);
        }
    }
}
