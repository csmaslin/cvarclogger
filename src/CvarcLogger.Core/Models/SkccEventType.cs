namespace CvarcLogger.Core.Models;

/// <summary>SKCC operating event types. Each has different scoring rules, multiplier strategies, and exchange requirements.</summary>
public enum SkccEventType
{
    /// <summary>SKS: Weekday Sprint. 2-hour CW sprint held monthly on Tuesday or Wednesday evening.</summary>
    WeekdaySprint,

    /// <summary>WES: Weekend Sprintathon. 36-hour relaxed sprint from Saturday through Sunday.</summary>
    WeekendSprintathon,

    /// <summary>SKSE: Europe Sprint. Regional variant for European time zones.</summary>
    EuropeSprint,

    /// <summary>SKSA: South America Sprint. Regional variant for South American time zones.</summary>
    SouthAmericaSprint,

    /// <summary>SKS-A: Asia Sprint. Regional variant for Asian time zones.</summary>
    AsiaSprint,

    /// <summary>SKCC-QSO: Annual QSO Party with expanded exchange (includes grid square).</summary>
    QsoParty,

    /// <summary>SSS: Slow Speed Saunter. Lower-tempo activity for beginners, no time pressure.</summary>
    SlowSpeedSaunter
}
