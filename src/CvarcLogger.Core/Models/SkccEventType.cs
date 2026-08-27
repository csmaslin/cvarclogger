namespace CvarcLogger.Core.Models;

/// <summary>SKCC operating event types. Each has different scoring rules, multiplier strategies, and exchange requirements.</summary>
public enum SkccEventType
{
    /// <summary>SKS: Weekday Sprint. 2-hour CW sprint held monthly on Tuesday or Wednesday evening.</summary>
    WeekdaySprint,

    /// <summary>WES: Weekend Sprintathon. 36-hour relaxed sprint from Saturday through Sunday.</summary>
    WeekendSprintathon,

    /// <summary>SKSE: Europe Sprint. Regional variant timed for European operators, officially open to
    /// all licensed amateurs worldwide.</summary>
    EuropeSprint,

    /// <summary>Not a confirmed official SKCC event -- skccgroup.com/operating_activities/sksa/ (which the
    /// original plan assumed was "South America") is actually the Asia Sprint, see AsiaSprint below.
    /// Kept as an enum member so it isn't a breaking rename, but SkccScorer does not support it: there is
    /// no confirmed South America-specific SKCC sprint to score against.</summary>
    SouthAmericaSprint,

    /// <summary>SKSA: Asia Sprint. Regional variant timed for ITU Region 3 (Asia) operators, officially
    /// open to all licensed amateurs worldwide. (Not "South America" -- corrected from the original plan
    /// after checking skccgroup.com/operating_activities/sksa/ directly.)</summary>
    AsiaSprint,

    /// <summary>SKCC-QSO: Annual QSO Party with expanded exchange (includes grid square).</summary>
    QsoParty,

    /// <summary>SSS: Slow Speed Saunter. Lower-tempo activity for beginners, no time pressure.</summary>
    SlowSpeedSaunter
}
