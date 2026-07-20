namespace CvarcLogger.Core.Models;

/// <summary>A callsign/identity the user operates under (e.g. personal call vs. an ARES/club call). Selected during QSO entry and denormalized onto each Qso.</summary>
public class StationProfile
{
    public int Id { get; set; }

    public string Callsign { get; set; } = string.Empty;
    public string? OperatorCallsign { get; set; }
    public string? MyGridSquare { get; set; }
    public string? MyState { get; set; }
    public string? MyCounty { get; set; }

    /// <summary>Free-text station location description (e.g. "Downtown Clubhouse") -- distinct from
    /// MyGridSquare/MyState/MyCounty, which are structured location fields.</summary>
    public string? Qth { get; set; }
    /// <summary>Operator name/initials for this session -- distinct from OperatorCallsign, for stations
    /// where multiple people log QSOs under the same callsign (e.g. a club station).</summary>
    public string? Op { get; set; }

    /// <summary>Fixed UTC offset in hours (e.g. -5 for Eastern, +5.5 for India) used to compute the
    /// station's local time for display -- see Qso.LocalDateTimeOn. Not date-aware; the operator
    /// flips <see cref="ObservesDaylightSavingTime"/> manually as their local DST status changes.</summary>
    public decimal UtcOffsetHours { get; set; }
    public bool ObservesDaylightSavingTime { get; set; }

    public bool IsDefault { get; set; }
    public string? Notes { get; set; }
}
