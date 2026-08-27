namespace CvarcLogger.Core.Rig;

public enum RigConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

/// <summary>Result of a connect attempt. Never throws — mirrors CallsignLookupResult's
/// Found/Error pattern for best-effort external I/O.</summary>
public record RigConnectResult(bool Success, string? Error = null)
{
    public static RigConnectResult Ok() => new(true);
    public static RigConnectResult Failed(string error) => new(false, error);
}

/// <summary>Result of one poll cycle (frequency + mode + power). FrequencyMhz matches Qso.FrequencyMhz's
/// unit (decimal MHz). RawMode is the untranslated rigctld mode string; MappedMode is the
/// CvarcLogger Modes-list value; SubMode is the derived Sub-Mode (currently only ever USB/LSB for SSB,
/// see RigModeMapper.ToCvarcLoggerSubMode), or null when CAT can't tell; Band is the derived band, or
/// null if outside all recognized bands. PowerFraction is the rig's RFPOWER level, 0.0-1.0, or null if
/// the rig doesn't expose it over CAT -- most rigs don't report real watts, only this fraction of their
/// own power-control range (see RadioProfile.MaxPowerWatts for turning it into an estimated wattage).</summary>
public record RigPollResult(bool Success, decimal? FrequencyMhz = null, string? RawMode = null,
    string? MappedMode = null, string? SubMode = null, string? Band = null, decimal? PowerFraction = null, string? Error = null)
{
    public static RigPollResult Ok(decimal freqMhz, string rawMode, string mappedMode, string? subMode, string? band, decimal? powerFraction = null) =>
        new(true, freqMhz, rawMode, mappedMode, subMode, band, powerFraction);
    public static RigPollResult Failed(string error) => new(false, Error: error);
}

/// <summary>TCP client of a running rigctld instance (Hamlib). Never throws for connection or
/// protocol failures — all failure modes surface as a false Success flag with an Error message,
/// so a UI poll loop never needs try/catch to stay safe.</summary>
public interface IRigControlService : IAsyncDisposable
{
    RigConnectionState State { get; }
    Task<RigConnectResult> ConnectAsync(string host, int port, CancellationToken ct = default);
    Task DisconnectAsync();
    Task<RigPollResult> PollAsync(CancellationToken ct = default);
}
