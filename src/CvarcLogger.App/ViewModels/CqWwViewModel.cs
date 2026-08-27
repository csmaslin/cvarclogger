using CommunityToolkit.Mvvm.ComponentModel;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Awards;
using CvarcLogger.Core.Models;
using CvarcLogger.Core.Scoring;

namespace CvarcLogger.App.ViewModels;

/// <summary>Tracks CQ WW DX Contest scoring for both SSB (last full weekend of October) and CW (last
/// full weekend of November) events. Each event runs Saturday 00:00 UTC through Sunday 23:59:59 UTC.
/// Score = QSO points x (Zone multiplier + Country multiplier), each multiplier counted once per band
/// across the six scoring bands (160/80/40/20/15/10m). See CqWwScorer for the point/multiplier rules.</summary>
public partial class CqWwViewModel : ObservableObject
{
    private readonly IQsoRepository _qsoRepository;
    private readonly ICallsignEntityResolver _entityResolver;

    private static readonly HashSet<string> SsbModes = new(StringComparer.OrdinalIgnoreCase) { "SSB", "USB", "LSB", "AM", "FM", "PH" };
    private static readonly HashSet<string> CwModes = new(StringComparer.OrdinalIgnoreCase) { "CW" };

    [ObservableProperty] private int ssbQsoCount;
    [ObservableProperty] private int ssbQsoPoints;
    [ObservableProperty] private int ssbZoneMultiplier;
    [ObservableProperty] private int ssbCountryMultiplier;
    [ObservableProperty] private int ssbScore;

    [ObservableProperty] private int cwQsoCount;
    [ObservableProperty] private int cwQsoPoints;
    [ObservableProperty] private int cwZoneMultiplier;
    [ObservableProperty] private int cwCountryMultiplier;
    [ObservableProperty] private int cwScore;

    [ObservableProperty] private int totalQsoCount;
    [ObservableProperty] private int totalScore;

    public CqWwViewModel(IQsoRepository qsoRepository, ICallsignEntityResolver entityResolver)
    {
        _qsoRepository = qsoRepository;
        _entityResolver = entityResolver;
    }

    public async Task LoadAsync(int year)
    {
        var allQsos = await _qsoRepository.GetAllAsync();

        // Own-station DXCC entity isn't denormalized per QSO anywhere (unlike MyState/MyGrid), so it's
        // resolved here from each distinct StationCallsign via the same resolver QsoEntryViewModel uses
        // for the worked station -- resolved once per distinct callsign, not once per QSO.
        var myEntityByCallsign = new Dictionary<string, DxccEntity?>(StringComparer.OrdinalIgnoreCase);
        foreach (var callsign in allQsos.Select(q => q.StationCallsign).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            myEntityByCallsign[callsign] = await _entityResolver.ResolveAsync(callsign);
        }

        var (ssbStart, ssbEnd) = GetCqWwEventDates(year, 10);
        var ssbQsos = allQsos.Where(q =>
            q.QsoDateTimeOnUtc >= ssbStart && q.QsoDateTimeOnUtc <= ssbEnd &&
            SsbModes.Contains(q.Mode));
        var ssbBreakdown = CqWwScorer.Score(ssbQsos, myEntityByCallsign);
        SsbQsoCount = ssbBreakdown.QsoCount;
        SsbQsoPoints = ssbBreakdown.QsoPoints;
        SsbZoneMultiplier = ssbBreakdown.ZoneMultiplier;
        SsbCountryMultiplier = ssbBreakdown.CountryMultiplier;
        SsbScore = ssbBreakdown.Score;

        var (cwStart, cwEnd) = GetCqWwEventDates(year, 11);
        var cwQsos = allQsos.Where(q =>
            q.QsoDateTimeOnUtc >= cwStart && q.QsoDateTimeOnUtc <= cwEnd &&
            CwModes.Contains(q.Mode));
        var cwBreakdown = CqWwScorer.Score(cwQsos, myEntityByCallsign);
        CwQsoCount = cwBreakdown.QsoCount;
        CwQsoPoints = cwBreakdown.QsoPoints;
        CwZoneMultiplier = cwBreakdown.ZoneMultiplier;
        CwCountryMultiplier = cwBreakdown.CountryMultiplier;
        CwScore = cwBreakdown.Score;

        TotalQsoCount = ssbBreakdown.QsoCount + cwBreakdown.QsoCount;
        TotalScore = ssbBreakdown.Score + cwBreakdown.Score;
    }

    /// <summary>Last full Saturday-Sunday weekend of the given month: the Saturday immediately before
    /// the month's last Sunday, through 23:59:59 UTC that Sunday.</summary>
    private static (DateTime start, DateTime end) GetCqWwEventDates(int year, int month)
    {
        int daysInMonth = DateTime.DaysInMonth(year, month);
        var lastSunday = new DateTime(year, month, daysInMonth, 0, 0, 0, DateTimeKind.Utc);
        while (lastSunday.DayOfWeek != DayOfWeek.Sunday)
            lastSunday = lastSunday.AddDays(-1);

        var saturday = lastSunday.AddDays(-1);
        var end = lastSunday.AddHours(23).AddMinutes(59).AddSeconds(59);
        return (saturday, end);
    }
}
