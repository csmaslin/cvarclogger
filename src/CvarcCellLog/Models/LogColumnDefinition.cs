using System.Globalization;
using CvarcLogger.Core.Models;

namespace CvarcCellLog.Models;

/// <summary>One column the QSO Log's table view can show -- a display header plus a relative
/// (star-sized) width. Star-sizing rather than fixed device-independent widths means the row never
/// needs horizontal scrolling: however many columns are active, they always divide up the screen
/// width between them (see QsoLogPage.xaml.cs's dynamic Grid/DataTemplate construction).</summary>
public record LogColumnDefinition(LogColumnKey Key, string Header, double Weight);

/// <summary>The full catalog of available columns, their default weights/order, and how to pull each
/// one's display text out of a Qso.</summary>
public static class LogColumns
{
    public static readonly IReadOnlyList<LogColumnDefinition> All = new[]
    {
        new LogColumnDefinition(LogColumnKey.Callsign, "Callsign", 1.6),
        new LogColumnDefinition(LogColumnKey.DateTimeLocal, "Date/Time", 2.2),
        new LogColumnDefinition(LogColumnKey.DateTimeUtc, "Date/Time (UTC)", 2.2),
        new LogColumnDefinition(LogColumnKey.TimeOff, "Time Off (UTC)", 2.2),
        new LogColumnDefinition(LogColumnKey.Band, "Band", 0.9),
        new LogColumnDefinition(LogColumnKey.Mode, "Mode", 1.0),
        new LogColumnDefinition(LogColumnKey.SubMode, "Sub-Mode", 1.1),
        new LogColumnDefinition(LogColumnKey.Frequency, "Freq (MHz)", 1.2),
        new LogColumnDefinition(LogColumnKey.TxPower, "TXp", 0.8),
        new LogColumnDefinition(LogColumnKey.RstSent, "RST/S", 0.8),
        new LogColumnDefinition(LogColumnKey.RstRcvd, "RST/R", 0.8),
        new LogColumnDefinition(LogColumnKey.Name, "Name", 1.4),
        new LogColumnDefinition(LogColumnKey.GridSquare, "Grid", 1.0),
        new LogColumnDefinition(LogColumnKey.City, "City", 1.3),
        new LogColumnDefinition(LogColumnKey.State, "State", 0.8),
        new LogColumnDefinition(LogColumnKey.County, "County", 1.2),
        new LogColumnDefinition(LogColumnKey.Country, "Country", 1.4),
        new LogColumnDefinition(LogColumnKey.ArrlSection, "Section", 1.0),
        new LogColumnDefinition(LogColumnKey.CqZone, "CQ", 0.6),
        new LogColumnDefinition(LogColumnKey.ItuZone, "ITU", 0.6),
        new LogColumnDefinition(LogColumnKey.Comment, "Comment", 1.6),
        new LogColumnDefinition(LogColumnKey.StationCallsign, "Station", 1.2),
    };

    /// <summary>Shown the first time the app runs, before the user has picked their own set --
    /// mirrors what the QSO Log showed before columns became configurable.</summary>
    public static readonly IReadOnlyList<LogColumnKey> DefaultOrder = new[]
    {
        LogColumnKey.Callsign, LogColumnKey.DateTimeLocal, LogColumnKey.Band, LogColumnKey.Mode, LogColumnKey.Name,
    };

    public static LogColumnDefinition Get(LogColumnKey key) => All.First(c => c.Key == key);

    public static string GetValue(Qso qso, LogColumnKey key) => key switch
    {
        LogColumnKey.Callsign => qso.Callsign,
        LogColumnKey.DateTimeUtc => qso.QsoDateTimeOnUtc.ToString("g", CultureInfo.CurrentCulture),
        LogColumnKey.DateTimeLocal => qso.LocalDateTimeOn.ToString("g", CultureInfo.CurrentCulture),
        LogColumnKey.TimeOff => qso.QsoDateTimeOffUtc?.ToString("g", CultureInfo.CurrentCulture) ?? "",
        LogColumnKey.Band => qso.Band,
        LogColumnKey.Mode => qso.Mode,
        LogColumnKey.SubMode => qso.SubMode ?? "",
        LogColumnKey.Frequency => qso.FrequencyMhz?.ToString("0.######", CultureInfo.InvariantCulture) ?? "",
        LogColumnKey.TxPower => qso.TxPowerWatts?.ToString("0.######", CultureInfo.InvariantCulture) ?? "",
        LogColumnKey.RstSent => qso.RstSent ?? "",
        LogColumnKey.RstRcvd => qso.RstRcvd ?? "",
        LogColumnKey.Name => qso.Name ?? "",
        LogColumnKey.GridSquare => qso.GridSquare ?? "",
        LogColumnKey.City => qso.City ?? "",
        LogColumnKey.State => qso.State ?? "",
        LogColumnKey.County => qso.County ?? "",
        LogColumnKey.Country => qso.Country ?? "",
        LogColumnKey.ArrlSection => qso.ArrlSection ?? "",
        LogColumnKey.CqZone => qso.CqZone?.ToString(CultureInfo.InvariantCulture) ?? "",
        LogColumnKey.ItuZone => qso.ItuZone?.ToString(CultureInfo.InvariantCulture) ?? "",
        LogColumnKey.Comment => qso.Comment ?? "",
        LogColumnKey.StationCallsign => qso.StationCallsign,
        _ => "",
    };

    /// <summary>The raw (unformatted) value a column sorts by -- deliberately not GetValue's display
    /// string, since formatting Date/Time or Frequency as text would sort them out of numeric/
    /// chronological order. See QsoLogViewModel.QsoColumnComparer for how this gets used.</summary>
    public static IComparable? GetSortKey(Qso qso, LogColumnKey key) => key switch
    {
        LogColumnKey.Callsign => qso.Callsign,
        LogColumnKey.DateTimeUtc => qso.QsoDateTimeOnUtc,
        LogColumnKey.DateTimeLocal => qso.LocalDateTimeOn,
        LogColumnKey.TimeOff => qso.QsoDateTimeOffUtc,
        LogColumnKey.Band => qso.Band,
        LogColumnKey.Mode => qso.Mode,
        LogColumnKey.SubMode => qso.SubMode,
        LogColumnKey.Frequency => qso.FrequencyMhz,
        LogColumnKey.TxPower => qso.TxPowerWatts,
        LogColumnKey.RstSent => qso.RstSent,
        LogColumnKey.RstRcvd => qso.RstRcvd,
        LogColumnKey.Name => qso.Name,
        LogColumnKey.GridSquare => qso.GridSquare,
        LogColumnKey.City => qso.City,
        LogColumnKey.State => qso.State,
        LogColumnKey.County => qso.County,
        LogColumnKey.Country => qso.Country,
        LogColumnKey.ArrlSection => qso.ArrlSection,
        LogColumnKey.CqZone => qso.CqZone,
        LogColumnKey.ItuZone => qso.ItuZone,
        LogColumnKey.Comment => qso.Comment,
        LogColumnKey.StationCallsign => qso.StationCallsign,
        _ => null,
    };
}
