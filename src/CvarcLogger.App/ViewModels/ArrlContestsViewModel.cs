using CommunityToolkit.Mvvm.ComponentModel;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Awards;
using CvarcLogger.Core.Models;
using CvarcLogger.Core.Scoring;

namespace CvarcLogger.App.ViewModels;

/// <summary>Tracks scoring for four ARRL-branded HF contests beyond Sweepstakes/Field Day/VHF (which
/// have their own tabs): the ARRL DX Contest (CW + Phone), the 10-Meter Contest, the 160-Meter Contest,
/// and the RTTY Roundup. All four have fixed weekend-of-month schedules, so a single year field derives
/// every event date automatically -- no manual date entry needed, unlike the Sprints tab.</summary>
public partial class ArrlContestsViewModel : ObservableObject
{
    private readonly IQsoRepository _qsoRepository;
    private readonly ICallsignEntityResolver _entityResolver;

    [ObservableProperty] private int dxCwQsoCount;
    [ObservableProperty] private int dxCwQsoPoints;
    [ObservableProperty] private int dxCwMultiplier;
    [ObservableProperty] private int dxCwScore;

    [ObservableProperty] private int dxPhoneQsoCount;
    [ObservableProperty] private int dxPhoneQsoPoints;
    [ObservableProperty] private int dxPhoneMultiplier;
    [ObservableProperty] private int dxPhoneScore;

    [ObservableProperty] private int tenMeterQsoCount;
    [ObservableProperty] private int tenMeterQsoPoints;
    [ObservableProperty] private int tenMeterMultiplier;
    [ObservableProperty] private int tenMeterScore;

    [ObservableProperty] private int oneSixtyQsoCount;
    [ObservableProperty] private int oneSixtyQsoPoints;
    [ObservableProperty] private int oneSixtyMultiplier;
    [ObservableProperty] private int oneSixtyScore;

    [ObservableProperty] private int rttyQsoCount;
    [ObservableProperty] private int rttyMultiplier;
    [ObservableProperty] private int rttyScore;

    public ArrlContestsViewModel(IQsoRepository qsoRepository, ICallsignEntityResolver entityResolver)
    {
        _qsoRepository = qsoRepository;
        _entityResolver = entityResolver;
    }

    public async Task LoadAsync(int year)
    {
        var allQsos = await _qsoRepository.GetAllAsync();

        var myEntityByCallsign = new Dictionary<string, DxccEntity?>(StringComparer.OrdinalIgnoreCase);
        foreach (var callsign in allQsos.Select(q => q.StationCallsign).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            myEntityByCallsign[callsign] = await _entityResolver.ResolveAsync(callsign);
        }

        // ARRL DX: CW third full weekend of February, Phone first full weekend of March. Both 0000 UTC
        // Saturday through 2359 UTC Sunday.
        var (dxCwStart, dxCwEnd) = GetWeekendWindow(year, 2, 3, DayOfWeek.Saturday, 0, 0, DayOfWeek.Sunday, 23, 59, 59);
        var dxCwQsos = allQsos.Where(q => q.QsoDateTimeOnUtc >= dxCwStart && q.QsoDateTimeOnUtc <= dxCwEnd && string.Equals(q.Mode, "CW", StringComparison.OrdinalIgnoreCase));
        var dxCw = ArrlDxScorer.Score(dxCwQsos, myEntityByCallsign);
        DxCwQsoCount = dxCw.QsoCount; DxCwQsoPoints = dxCw.QsoPoints; DxCwMultiplier = dxCw.Multiplier; DxCwScore = dxCw.Score;

        var (dxPhoneStart, dxPhoneEnd) = GetWeekendWindow(year, 3, 1, DayOfWeek.Saturday, 0, 0, DayOfWeek.Sunday, 23, 59, 59);
        var dxPhoneQsos = allQsos.Where(q => q.QsoDateTimeOnUtc >= dxPhoneStart && q.QsoDateTimeOnUtc <= dxPhoneEnd && IsPhoneMode(q.Mode));
        var dxPhone = ArrlDxScorer.Score(dxPhoneQsos, myEntityByCallsign);
        DxPhoneQsoCount = dxPhone.QsoCount; DxPhoneQsoPoints = dxPhone.QsoPoints; DxPhoneMultiplier = dxPhone.Multiplier; DxPhoneScore = dxPhone.Score;

        // 10-Meter Contest: second full weekend of December, 0000 UTC Saturday through 2359 UTC Sunday.
        var (tenStart, tenEnd) = GetWeekendWindow(year, 12, 2, DayOfWeek.Saturday, 0, 0, DayOfWeek.Sunday, 23, 59, 59);
        var tenQsos = allQsos.Where(q => q.QsoDateTimeOnUtc >= tenStart && q.QsoDateTimeOnUtc <= tenEnd);
        var ten = TenMeterScorer.Score(tenQsos);
        TenMeterQsoCount = ten.QsoCount; TenMeterQsoPoints = ten.QsoPoints; TenMeterMultiplier = ten.PhoneMultiplier + ten.CwMultiplier; TenMeterScore = ten.Score;

        // 160-Meter Contest: first full weekend of December, 2200 UTC Friday through 1559 UTC Sunday
        // (the one contest here that starts Friday, not Saturday).
        var (oneSixtyStart, oneSixtyEnd) = GetWeekendWindow(year, 12, 1, DayOfWeek.Friday, 22, 0, DayOfWeek.Sunday, 15, 59, 0);
        var oneSixtyQsos = allQsos.Where(q => q.QsoDateTimeOnUtc >= oneSixtyStart && q.QsoDateTimeOnUtc <= oneSixtyEnd);
        var oneSixty = OneSixtyMeterScorer.Score(oneSixtyQsos, myEntityByCallsign);
        OneSixtyQsoCount = oneSixty.QsoCount; OneSixtyQsoPoints = oneSixty.QsoPoints; OneSixtyMultiplier = oneSixty.Multiplier; OneSixtyScore = oneSixty.Score;

        // RTTY Roundup: first full weekend of January, but never on January 1 -- if the first Saturday of
        // January falls on the 1st, the contest moves to the following Saturday instead. 1800 UTC
        // Saturday through 2359 UTC Sunday.
        var (rttyStart, rttyEnd) = GetRttyRoundupWindow(year);
        var rttyQsos = allQsos.Where(q => q.QsoDateTimeOnUtc >= rttyStart && q.QsoDateTimeOnUtc <= rttyEnd);
        var rtty = RttyRoundupScorer.Score(rttyQsos);
        RttyQsoCount = rtty.QsoCount; RttyMultiplier = rtty.Multiplier; RttyScore = rtty.Score;
    }

    private static readonly HashSet<string> PhoneModes = new(StringComparer.OrdinalIgnoreCase) { "SSB", "USB", "LSB", "AM", "FM", "PH" };
    private static bool IsPhoneMode(string mode) => PhoneModes.Contains(mode);

    /// <summary>The Nth occurrence of startDay in startMonth (1-indexed) at the given UTC time, through
    /// the following endDay at its own UTC time -- generalizes the "Nth full weekend of month" pattern
    /// used across every ARRL contest here, since each one anchors to a different day of week and time
    /// (most start Saturday 0000/1800 UTC, but the 160-Meter Contest starts Friday 2200 UTC).</summary>
    private static (DateTime start, DateTime end) GetWeekendWindow(
        int year, int month, int nth, DayOfWeek startDay, int startHour, int startMinute,
        DayOfWeek endDay, int endHour, int endMinute, int endSecond)
    {
        var firstOfMonth = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var firstOccurrence = firstOfMonth.AddDays(((int)startDay - (int)firstOfMonth.DayOfWeek + 7) % 7);
        var anchorDay = firstOccurrence.AddDays((nth - 1) * 7);

        var start = anchorDay.AddHours(startHour).AddMinutes(startMinute);

        var end = anchorDay;
        while (end.DayOfWeek != endDay)
            end = end.AddDays(1);
        end = end.AddHours(endHour).AddMinutes(endMinute).AddSeconds(endSecond);

        return (start, end);
    }

    private static (DateTime start, DateTime end) GetRttyRoundupWindow(int year)
    {
        var jan1 = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var firstSaturday = jan1.AddDays((6 - (int)jan1.DayOfWeek) % 7);
        if (firstSaturday == jan1)
            firstSaturday = firstSaturday.AddDays(7);

        var start = firstSaturday.AddHours(18);
        var end = firstSaturday.AddDays(1).AddHours(23).AddMinutes(59).AddSeconds(59);
        return (start, end);
    }
}
