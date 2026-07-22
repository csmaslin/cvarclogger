namespace CvarcLogger.App.Services;

// Internet (network) CAT support for the Elecraft K4's native TCP command protocol. Ported from the
// companion CvarcCellLog (MAUI) app's Services folder -- pure .NET, no MAUI dependency, depends only on
// CvarcLogger.Core.Rig (RigModeMapper, BandCalculator) which this project already references.

public enum K4ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Error,
}

/// <summary>Result of a connect attempt. Never throws -- mirrors the Success/Error result pattern used
/// throughout this app's lookup/CAT-adjacent code for best-effort external I/O.</summary>
public record K4ConnectResult(bool Success, string? Error = null)
{
    public static K4ConnectResult Ok() => new(true);
    public static K4ConnectResult Failed(string error) => new(false, error);
}

/// <summary>Result of one poll (Frequency + Mode + Power). MappedMode/SubMode are already translated into
/// this app's own Mode vocabulary (via K4ModeAdapter + CvarcLogger.Core.Rig.RigModeMapper) -- null when
/// the radio reported an unrecognized/N-A mode code rather than when the poll itself failed. PowerWatts is
/// the configured power *setting* (from PCX;), not a live TX-only wattmeter reading -- null if the K4's
/// reply couldn't be parsed.</summary>
public record K4PollResult(bool Success, decimal? FrequencyMhz = null, string? MappedMode = null,
    string? SubMode = null, string? Band = null, decimal? PowerWatts = null, string? Error = null)
{
    public static K4PollResult Ok(decimal freqMhz, string? mappedMode, string? subMode, string? band, decimal? powerWatts) =>
        new(true, freqMhz, mappedMode, subMode, band, powerWatts);
    public static K4PollResult Failed(string error) => new(false, Error: error);
}
