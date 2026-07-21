namespace CvarcCellLog.Models;

/// <summary>Every field the QSO Log's table view can show as a column. Not every field a Qso has --
/// just the ones useful as a compact, single-line log column (see LogColumns.GetValue).</summary>
public enum LogColumnKey
{
    Callsign,
    DateTimeUtc,
    DateTimeLocal,
    TimeOff,
    Band,
    Mode,
    SubMode,
    Frequency,
    TxPower,
    RstSent,
    RstRcvd,
    Name,
    GridSquare,
    City,
    State,
    County,
    Country,
    ArrlSection,
    CqZone,
    ItuZone,
    Comment,
    StationCallsign,
}
