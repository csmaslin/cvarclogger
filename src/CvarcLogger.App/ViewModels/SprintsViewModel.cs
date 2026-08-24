using CommunityToolkit.Mvvm.ComponentModel;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Scoring;

namespace CvarcLogger.App.ViewModels;

/// <summary>Tracks NCJ-style Sprint scoring (CW, SSB, RTTY) on 80/40/20m, per event window. Unlike CQ WW
/// or Sweepstakes, Sprint dates aren't a fixed "Nth weekend" formula, so each section takes its own
/// explicit UTC start/end instead of being derived from a year. VHF/UHF Sprint (6m/2m/222/432/902+) is a
/// separate scoring scheme not yet specified -- its section is a placeholder until that's defined.</summary>
public partial class SprintsViewModel : ObservableObject
{
    private readonly IQsoRepository _qsoRepository;

    private static readonly HashSet<string> SprintBands = new(StringComparer.OrdinalIgnoreCase) { "80m", "40m", "20m" };
    private static readonly HashSet<string> CwModes = new(StringComparer.OrdinalIgnoreCase) { "CW" };
    private static readonly HashSet<string> SsbModes = new(StringComparer.OrdinalIgnoreCase) { "SSB", "USB", "LSB", "AM", "FM" };
    private static readonly HashSet<string> RttyModes = new(StringComparer.OrdinalIgnoreCase) { "RTTY" };

    [ObservableProperty] private int cwQsoCount;
    [ObservableProperty] private int cwMultiplier;
    [ObservableProperty] private int cwScore;

    [ObservableProperty] private int ssbQsoCount;
    [ObservableProperty] private int ssbMultiplier;
    [ObservableProperty] private int ssbScore;

    [ObservableProperty] private int rttyQsoCount;
    [ObservableProperty] private int rttyMultiplier;
    [ObservableProperty] private int rttyScore;

    [ObservableProperty] private int vhfQsoCount;
    [ObservableProperty] private int vhfQsoPoints;
    [ObservableProperty] private int vhfGridMultiplier;
    [ObservableProperty] private int vhfScore;

    public SprintsViewModel(IQsoRepository qsoRepository)
    {
        _qsoRepository = qsoRepository;
    }

    public async Task ScoreCwAsync(DateTime startUtc, DateTime endUtc)
    {
        var breakdown = await ScoreEventAsync(startUtc, endUtc, CwModes);
        CwQsoCount = breakdown.QsoCount;
        CwMultiplier = breakdown.Multiplier;
        CwScore = breakdown.Score;
    }

    public async Task ScoreSsbAsync(DateTime startUtc, DateTime endUtc)
    {
        var breakdown = await ScoreEventAsync(startUtc, endUtc, SsbModes);
        SsbQsoCount = breakdown.QsoCount;
        SsbMultiplier = breakdown.Multiplier;
        SsbScore = breakdown.Score;
    }

    public async Task ScoreRttyAsync(DateTime startUtc, DateTime endUtc)
    {
        var breakdown = await ScoreEventAsync(startUtc, endUtc, RttyModes);
        RttyQsoCount = breakdown.QsoCount;
        RttyMultiplier = breakdown.Multiplier;
        RttyScore = breakdown.Score;
    }

    private async Task<SprintScoreBreakdown> ScoreEventAsync(DateTime startUtc, DateTime endUtc, IReadOnlySet<string> modes)
    {
        var allQsos = await _qsoRepository.GetAllAsync();
        var eventQsos = allQsos.Where(q =>
            q.QsoDateTimeOnUtc >= startUtc && q.QsoDateTimeOnUtc <= endUtc &&
            modes.Contains(q.Mode));
        return SprintScorer.Score(eventQsos, SprintBands);
    }

    /// <summary>Scores the ARRL VHF Contest (fixed-station category) for the given UTC window. Tier 3
    /// (902/1296 MHz) and tier 4 (2.3 GHz+) point values differ between January and June/September per
    /// the official rules -- pass isJanuary accordingly. All modes count; there's no mode filter here,
    /// unlike the CW/SSB/RTTY Sprint sections above.</summary>
    public async Task ScoreVhfAsync(DateTime startUtc, DateTime endUtc, bool isJanuary)
    {
        var allQsos = await _qsoRepository.GetAllAsync();
        var eventQsos = allQsos.Where(q => q.QsoDateTimeOnUtc >= startUtc && q.QsoDateTimeOnUtc <= endUtc);

        (int tier3Points, int tier4Points) = isJanuary ? (4, 8) : (3, 4);
        var breakdown = VhfContestScorer.Score(eventQsos, tier3Points, tier4Points);

        VhfQsoCount = breakdown.QsoCount;
        VhfQsoPoints = breakdown.QsoPoints;
        VhfGridMultiplier = breakdown.GridSquareMultiplier;
        VhfScore = breakdown.Score;
    }
}
